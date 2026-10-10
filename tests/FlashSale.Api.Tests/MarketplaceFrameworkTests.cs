using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FlashSale.Api.Connectors;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlashSale.Api.Tests;

public sealed class MarketplaceFrameworkTests
{
    private sealed class FakeSource(Marketplace marketplace, bool configured, bool fails = false) : IMarketplaceSource
    {
        public string Name => $"{marketplace} fake";
        public Marketplace Marketplace => marketplace;
        public bool IsConfigured => configured;
        public TimeSpan Interval => TimeSpan.FromMinutes(15);
        public ConnectorCapability Capability => configured
            ? new(marketplace, ConnectorAccess.OfficialApi, true, "fake")
            : MarketplaceCapabilities.Unavailable(marketplace);

        public async IAsyncEnumerable<FeedProduct> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (fails) throw new InvalidOperationException("feed is down");
            yield return new FeedProduct($"{marketplace}-1", "default", $"{marketplace} item", "default", "Test", null, null, null,
                99.5m, "THB", DateTimeOffset.UtcNow.AddMinutes(-1), $"obs-{marketplace}", true);
        }
    }

    private static ServiceProvider Services(params IMarketplaceSource[] sources)
    {
        var services = new ServiceCollection();
        var database = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DataMutationLock>();
        services.AddDbContext<FlashSaleDbContext>(options => options.UseInMemoryDatabase(database));
        services.AddScoped<AlertEvaluator>();
        services.AddScoped<FeedImporter>();
        foreach (var source in sources) services.AddScoped<IMarketplaceSource>(_ => source);
        return services.BuildServiceProvider();
    }

    private static MarketplaceIngestService Scheduler(ServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MarketplaceIngestService>.Instance);

    private static async Task<List<Marketplace>> StoredMarketplaces(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>().Products.Select(p => p.Marketplace).ToListAsync();
    }

    [Fact]
    public async Task StatusListsEveryMarketplaceWithWhatItIsWaitingFor()
    {
        await using var factory = new ApiFactory();
        var status = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/status");
        var byName = status.GetProperty("marketplaces").EnumerateArray()
            .ToDictionary(x => x.GetProperty("marketplace").GetString()!, x => x);

        Assert.Equal(["Lazada", "Shopee", "TikTok"], byName.Keys.Order());
        Assert.Equal("SampleFeed", byName["Lazada"].GetProperty("access").GetString());
        foreach (var name in new[] { "Shopee", "TikTok" })
        {
            Assert.Equal("Unavailable", byName[name].GetProperty("access").GetString());
            Assert.Contains("docs/marketplaces.md", byName[name].GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task OnlyConfiguredSourcesAreImported()
    {
        await using var provider = Services(new FakeSource(Marketplace.Lazada, configured: true), new FakeSource(Marketplace.Shopee, configured: false));
        var scheduler = Scheduler(provider);
        await scheduler.StartAsync(CancellationToken.None);
        for (var i = 0; i < 100 && (await StoredMarketplaces(provider)).Count == 0; i++) await Task.Delay(50);
        await scheduler.StopAsync(CancellationToken.None);

        Assert.Equal([Marketplace.Lazada], await StoredMarketplaces(provider));
    }

    [Fact]
    public async Task AFailingMarketplaceDoesNotStopTheOthers()
    {
        await using var provider = Services(new FakeSource(Marketplace.Lazada, configured: true, fails: true), new FakeSource(Marketplace.Shopee, configured: true));
        var scheduler = Scheduler(provider);
        await scheduler.StartAsync(CancellationToken.None);
        for (var i = 0; i < 100 && (await StoredMarketplaces(provider)).Count == 0; i++) await Task.Delay(50);
        await scheduler.StopAsync(CancellationToken.None);

        Assert.Equal([Marketplace.Shopee], await StoredMarketplaces(provider));
    }

    [Fact]
    public async Task ImportingTwiceDoesNotDuplicate()
    {
        await using var provider = Services(new FakeSource(Marketplace.TikTok, configured: true));
        var scheduler = Scheduler(provider);
        await scheduler.RunOnceAsync("TikTok fake", CancellationToken.None);
        await scheduler.RunOnceAsync("TikTok fake", CancellationToken.None);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>();
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(1, await db.PriceObservations.CountAsync());
        Assert.False((await db.Products.SingleAsync()).IsSample);
    }

    [Fact]
    public void AnUnconfiguredSourceReportsUnavailable()
    {
        var capability = new FakeSource(Marketplace.Shopee, configured: false).Capability;
        Assert.Equal(ConnectorAccess.Unavailable, capability.Access);
        Assert.Contains("docs/marketplaces.md", capability.Status);
    }
}
