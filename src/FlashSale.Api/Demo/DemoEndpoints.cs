using System.Runtime.CompilerServices;
using FlashSale.Api.Connectors;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Demo;

public sealed record TickRequest(Guid ProductId);

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        app.MapPost("/api/demo/tick", async (TickRequest request, FlashSaleDbContext db,
            FeedImporter importer, TimeProvider clock, CancellationToken ct) =>
        {
            var product = await db.Products.AsNoTracking().Include(x => x.Observations).SingleOrDefaultAsync(x => x.Id == request.ProductId && x.IsSample, ct);
            if (product is null) return Results.NotFound();
            var current = product.Observations.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId).FirstOrDefault();
            if (current is null) return Results.Problem(statusCode: 409, title: "No sample observation to simulate.");
            var captured = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
            if (captured <= current.CapturedAt) return Results.Problem(statusCode: 409, title: "Wait a second before the next sample simulation.");
            var price = decimal.Round(current.Price * 0.8m, 2);
            var row = new FeedProduct(product.ExternalProductId, product.ExternalVariantId, product.Title,
                product.VariantLabel, product.Category, null, null, product.ComparableVariantKey, price, "THB", captured,
                $"sample-tick-{captured.ToUnixTimeSeconds()}", true, product.OriginalPrice);
            await importer.ImportAsync(new TickFeed(product.Marketplace, row), ct);
            return Results.Ok(new { IsSample = true, Simulation = "Local 20% price decrease", ProductId = product.Id, Price = price, CapturedAt = captured });
        }).RequireAuthorization("user");
    }

    private sealed class TickFeed(Marketplace marketplace, FeedProduct row) : IMarketplaceConnector
    {
        public ConnectorCapability Capability => new(marketplace, ConnectorAccess.SampleFeed, false, "Local price simulation.");
        public async IAsyncEnumerable<FeedProduct> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
            await Task.CompletedTask;
        }
    }
}
