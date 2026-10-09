using System.Runtime.CompilerServices;
using FlashSale.Api.Catalog;
using FlashSale.Api.Connectors;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlashSale.Api.MariaDb.Tests;

public sealed class MariaDbRuntimeTests
{
    [Fact]
    public async Task MariaDbCreatesSchemaAndSupportsCatalogQueriesAndExactIdentifiers()
    {
        var connectionString = Environment.GetEnvironmentVariable("MARIADB_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "Set MARIADB_TEST_CONNECTION_STRING before running this integration test.");
        var clock = TimeProvider.System;
        var options = new DbContextOptionsBuilder<FlashSaleDbContext>()
            .UseMySql(connectionString!, new MariaDbServerVersion(new Version(11, 4, 0))).Options;
        await using var db = new FlashSaleDbContext(options);
        Assert.True(await db.Database.CanConnectAsync());

        var suffix = Guid.NewGuid().ToString("N");
        var capturedAt = DateTimeOffset.UtcNow.AddHours(-1).AddTicks(-3);
        var feed = new TestFeed(new FeedProduct(
            $"product-{suffix}", "variant", "Thai product", "Default", "Test", null, null, null,
            19.99m, "THB", capturedAt, $"observation-{suffix}", true));
        var importer = new FeedImporter(db, new DataMutationLock(), clock, new AlertEvaluator(db, clock));

        var first = await importer.ImportAsync(feed);
        var repeated = await importer.ImportAsync(feed);
        Assert.Equal(1, first.ObservationsAdded);
        Assert.Equal(1, repeated.ObservationsSkipped);
        var observation = await db.PriceObservations.SingleAsync();
        Assert.Equal(capturedAt.AddTicks(-(capturedAt.UtcTicks % 10)), observation.CapturedAt);
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE PriceObservations SET Price = {-1m} WHERE Id = {observation.Id}"));

        var users = new[]
        {
            new AppUser { Issuer = "https://issuer.example", Subject = $"Alice-{suffix}" },
            new AppUser { Issuer = "https://issuer.example", Subject = $"alice-{suffix}" }
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Users.CountAsync(x => x.Issuer == "https://issuer.example" && x.Subject.ToLower().Contains(suffix)));

        var productId = FeedImporter.ProductId(Marketplace.Lazada, $"product-{suffix}", "variant");
        var products = await ProductEndpoints.Project(db.Products).Where(x => x.Id == productId).ToListAsync();
        Assert.Equal(19.99m, Assert.Single(products).CurrentPrice);

        var product = await db.Products.SingleAsync(x => x.Id == productId);
        db.Products.Remove(product);
        db.Users.RemoveRange(users);
        await db.SaveChangesAsync();
        Assert.False(await db.PriceObservations.AnyAsync(x => x.ProductId == productId));
    }

    private sealed class TestFeed(FeedProduct row) : IMarketplaceConnector
    {
        public ConnectorCapability Capability => new(Marketplace.Lazada, ConnectorAccess.SampleFeed, true, "Runtime integration test");

        public async IAsyncEnumerable<FeedProduct> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return row;
        }
    }
}
