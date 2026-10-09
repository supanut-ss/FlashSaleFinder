using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using FlashSale.Api.Catalog;
using FlashSale.Api.Connectors;
using FlashSale.Api.Data;
using FlashSale.Api.Demo;
using FlashSale.Api.Domain;
using FlashSale.Api.Identity;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FlashSale.Api.Tests;

public sealed class PhaseTwoTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SampleImportsAreDeterministicIdempotentAndExplicitlyLabelled()
    {
        await using var factory = new ApiFactory(clock: new TestClock(Now));
        using var scope = factory.Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<FeedImporter>();
        var feed = scope.ServiceProvider.GetRequiredService<SampleFeed>();
        var first = await importer.ImportAsync(feed);
        var second = await importer.ImportAsync(feed);
        var db = scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>();
        Assert.Equal(6, first.ProductsAdded);
        Assert.Equal(380, first.ObservationsAdded);
        Assert.Equal(0, second.ObservationsAdded);
        Assert.Equal(380, second.ObservationsSkipped);
        Assert.Equal(380, await db.PriceObservations.CountAsync());
        Assert.All(await db.Products.ToListAsync(), product =>
        {
            Assert.True(product.IsSample);
            Assert.Equal(Marketplace.Lazada, product.Marketplace);
            Assert.Null(product.ProductUrl); Assert.Null(product.AffiliateUrl);
        });
        Assert.All(await db.PriceObservations.ToListAsync(), observation => Assert.Equal(TimeSpan.Zero, observation.CapturedAt.TimeOfDay));
    }

    [Fact]
    public async Task OlderObservationCannotReplaceCurrentPriceOrListingMetadata()
    {
        await using var factory = new ApiFactory(clock: new TestClock(Now));
        using var scope = factory.Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<FeedImporter>();
        await importer.ImportAsync(new TestFeed(Row("new", 100, Now, "Latest name")));
        await importer.ImportAsync(new TestFeed(Row("old", 50, Now.AddDays(-1), "Old name")));
        using var client = factory.CreateClient();
        var product = await client.GetFromJsonAsync<ProductDto>($"/api/products/{FeedImporter.ProductId(Marketplace.Lazada, "p", "v")}");
        Assert.Equal("Latest name", product!.Title);
        Assert.Equal(100, product.CurrentPrice);
    }

    [Theory]
    [InlineData("future")]
    [InlineData("currency")]
    [InlineData("duplicate")]
    public async Task InvalidFeedIsRejectedWithoutPartialImport(string mode)
    {
        await using var factory = new ApiFactory(clock: new TestClock(Now));
        using var scope = factory.Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<FeedImporter>();
        var row = Row("ok", 100, Now);
        var bad = mode switch
        {
            "future" => row with { SourceObservationId = "future", CapturedAt = Now.AddSeconds(1) },
            "currency" => row with { SourceObservationId = "usd", Currency = "USD" },
            _ => row with { Price = 200 }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ImportAsync(new TestFeed(row, bad)));
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>().Products.CountAsync());
    }

    [Fact]
    public async Task PersistedObservationIdCannotBeRewritten()
    {
        await using var factory = new ApiFactory(clock: new TestClock(Now));
        using var scope = factory.Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<FeedImporter>();
        var row = Row("immutable", 19.99m, Now);
        await importer.ImportAsync(new TestFeed(row));
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ImportAsync(new TestFeed(row with { Price = 10 })));
        Assert.Equal(19.99m, (await scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>().PriceObservations.SingleAsync()).Price);
    }

    [Fact]
    public async Task FeedTimestampsAreNormalizedToMariaDbMicrosecondPrecision()
    {
        await using var factory = new ApiFactory(clock: new TestClock(Now));
        using var scope = factory.Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<FeedImporter>();
        var row = Row("sub-microsecond", 19.99m, Now.AddTicks(-3));

        var first = await importer.ImportAsync(new TestFeed(row));
        var repeated = await importer.ImportAsync(new TestFeed(row));
        var stored = await scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>().PriceObservations.SingleAsync();

        Assert.Equal(1, first.ObservationsAdded);
        Assert.Equal(1, repeated.ObservationsSkipped);
        Assert.Equal(Now.AddTicks(-10), stored.CapturedAt);
    }

    [Fact]
    public void DailyBaselinesExcludeTodayAndDoNotOverweightIntradaySamples()
    {
        var product = Product();
        var observations = Enumerable.Range(1, 7).Select(day => Observation(100, Now.Date.AddDays(-day))).ToList();
        observations.Add(Observation(300, Now.Date.AddDays(-1).AddHours(10)));
        observations.Add(Observation(10000, Now.Date.AddHours(9)));
        observations.Add(Observation(50, Now.Date.AddHours(11)));
        var insight = DealInsights.Calculate(product, observations, Now);
        Assert.Equal(7, insight.PriorDays);
        Assert.Equal(114.29m, insight.Windows.Single(x => x.Days == 7).AveragePrice);
        Assert.Equal(100, insight.Windows.Single(x => x.Days == 7).LowestPrice);
        Assert.Equal(56.3m, insight.RealDiscountPercent);
        Assert.Null(insight.ProvisionalScore);
        Assert.Equal("insufficientSignals", insight.ScoreEvidence);
        Assert.True(insight.IsSample);
    }

    [Fact]
    public void MissingSparseAndStaleHistoryHaveNoSavingsOrScore()
    {
        var product = Product();
        var noHistory = DealInsights.Calculate(product, [], Now);
        Assert.Equal("noHistory", noHistory.Evidence);
        Assert.Null(noHistory.RealDiscountPercent);
        var sparse = DealInsights.Calculate(product, Enumerable.Range(0, 7).Select(day => Observation(100, Now.Date.AddDays(-day))), Now);
        Assert.Equal(6, sparse.PriorDays);
        Assert.Equal("insufficientHistory", sparse.Evidence);
        Assert.Null(sparse.RealDiscountPercent);
        var stale = DealInsights.Calculate(product, Enumerable.Range(2, 8).Select(day => Observation(100, Now.UtcDateTime.AddDays(-day))), Now);
        Assert.Equal("stale", stale.Evidence);
        Assert.False(stale.IsFresh);
        Assert.Null(stale.ProvisionalScore);
        Assert.Null(stale.RealDiscountPercent);
    }

    [Fact]
    public async Task WatchlistAndAlertsAreOwnedAndRepeatedChangesDoNotDuplicateEvents()
    {
        await using var factory = new ApiFactory(seed: true, clock: new TestClock(Now));
        using var alice = Client(factory, "alice");
        using var bob = Client(factory, "bob");
        var products = await alice.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products");
        var product = products!.Items.Single(x => x.ExternalProductId == "sample-headphones");
        var request = new WatchRequest(product.Id, 1000);
        var first = await alice.PostAsJsonAsync("/api/watchlist", request);
        first.EnsureSuccessStatusCode();
        var watched = (await first.Content.ReadFromJsonAsync<WatchDto>())!;
        (await alice.PostAsJsonAsync("/api/watchlist", request)).EnsureSuccessStatusCode();
        (await alice.PostAsJsonAsync("/api/watchlist", request with { TargetPrice = 1100 })).EnsureSuccessStatusCode();
        Assert.Single((await alice.GetFromJsonAsync<Items<WatchDto>>("/api/watchlist"))!.Values);
        Assert.Single((await alice.GetFromJsonAsync<Items<AlertDto>>("/api/alerts"))!.Values);
        Assert.Empty((await bob.GetFromJsonAsync<Items<WatchDto>>("/api/watchlist"))!.Values);
        Assert.Empty((await bob.GetFromJsonAsync<Items<AlertDto>>("/api/alerts"))!.Values);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/watchlist/{watched.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/api/watchlist/{watched.Id}")).StatusCode);
        Assert.Empty((await alice.GetFromJsonAsync<Items<AlertDto>>("/api/alerts"))!.Values);
    }

    [Fact]
    public async Task DemoTickCrossesTargetAndCannotProduceDuplicateAlert()
    {
        await using var factory = new ApiFactory(seed: true, clock: new TestClock(Now));
        using var client = Client(factory, "alice");
        var productId = FeedImporter.ProductId(Marketplace.Lazada, "sample-headphones", "black");
        (await client.PostAsJsonAsync("/api/watchlist", new WatchRequest(productId, 900))).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<Items<AlertDto>>("/api/alerts"))!.Values);
        (await client.PostAsJsonAsync("/api/demo/tick", new TickRequest(productId))).EnsureSuccessStatusCode();
        var events = (await client.GetFromJsonAsync<Items<AlertDto>>("/api/alerts"))!.Values;
        Assert.Equal(792, Assert.Single(events).Price);
        Assert.Equal("inApp", events[0].Channel);
        Assert.True(events[0].IsSample);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/demo/tick", new TickRequest(productId))).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<Items<AlertDto>>("/api/alerts"))!.Values);
    }

    [Fact]
    public async Task FollowOnlyHasNoAlertAndInvalidTargetIsRejected()
    {
        await using var factory = new ApiFactory(seed: true, clock: new TestClock(Now));
        using var client = Client(factory, "alice");
        var id = FeedImporter.ProductId(Marketplace.Lazada, "sample-headphones", "black");
        var response = await client.PostAsJsonAsync("/api/watchlist", new WatchRequest(id, null));
        response.EnsureSuccessStatusCode();
        var watched = (await response.Content.ReadFromJsonAsync<WatchDto>())!;
        Assert.Null(watched.TargetPrice); Assert.False(watched.AlertEnabled);
        Assert.Empty((await client.GetFromJsonAsync<Items<AlertDto>>("/api/alerts"))!.Values);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/watchlist", new WatchRequest(id, -1))).StatusCode);
    }

    [Fact]
    public async Task ParallelFirstRequestsCreateOneDemoUser()
    {
        await using var factory = new ApiFactory(seed: true, clock: new TestClock(Now));
        using var client = Client(factory, "parallel-user");
        var paths = Enumerable.Range(0, 16).Select(index => (index % 3) switch
        {
            0 => "/api/me",
            1 => "/api/watchlist",
            _ => "/api/alerts"
        });
        var responses = await Task.WhenAll(paths.Select(path => client.GetAsync(path)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.Issuer == IdentityConfiguration.DemoIssuer && x.Subject == "parallel-user"));
    }

    private static HttpClient Client(ApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        var key = factory.Services.GetRequiredService<SymmetricSecurityKey>();
        var token = new JwtSecurityToken(IdentityConfiguration.DemoIssuer, IdentityConfiguration.DemoAudience,
            [new Claim("sub", subject)], expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static FeedProduct Row(string source, decimal price, DateTimeOffset time, string title = "Sample test") =>
        new("p", "v", title, "Variant", "Test", null, null, null, price, "THB", time, source, true);
    private static Product Product() => new() { Title = "Test", VariantLabel = "Variant", Category = "Test", ExternalProductId = "p", ExternalVariantId = "v", IsSample = true };
    private static PriceObservation Observation(decimal price, DateTime captured) => new()
    { Price = price, CapturedAt = new DateTimeOffset(DateTime.SpecifyKind(captured, DateTimeKind.Utc)), SourceObservationId = Guid.NewGuid().ToString() };
    private sealed class TestClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed record Items<T>([property: System.Text.Json.Serialization.JsonPropertyName("items")] List<T> Values);
    private sealed class TestFeed(params FeedProduct[] rows) : IMarketplaceConnector
    {
        public ConnectorCapability Capability => new(Marketplace.Lazada, ConnectorAccess.SampleFeed, true, "Test fixture");
        public async IAsyncEnumerable<FeedProduct> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        { foreach (var row in rows) { cancellationToken.ThrowIfCancellationRequested(); yield return row; } await Task.CompletedTask; }
    }
}
