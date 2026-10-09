using System.Security.Cryptography;
using System.Text;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Connectors;

public sealed record ImportResult(int ProductsAdded, int ObservationsAdded, int ObservationsSkipped);

public sealed class FeedImporter(FlashSaleDbContext db, DataMutationLock mutationLock, TimeProvider clock, AlertEvaluator alerts)
{
    public async Task<ImportResult> ImportAsync(IMarketplaceConnector connector, CancellationToken ct = default)
    {
        if (connector.Capability.Access == ConnectorAccess.Unavailable) throw new ArgumentException("Connector access is unavailable.");
        var rows = new Dictionary<(string Product, string Variant, string Observation), FeedProduct>();
        await foreach (var row in connector.ReadAsync(ct))
        {
            Validate(row);
            var key = (row.ExternalProductId, row.ExternalVariantId, row.SourceObservationId);
            if (rows.TryGetValue(key, out var duplicate) && duplicate != row)
                throw new ArgumentException("Conflicting duplicate observation IDs in feed.");
            rows[key] = row;
        }
        await mutationLock.Semaphore.WaitAsync(ct);
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            var addedProducts = 0;
            var addedObservations = 0;
            var skipped = 0;
            var changedProducts = new HashSet<Guid>();
            // Validate persisted IDs before tracking changes, so malformed imports do not partially change the demo store.
            foreach (var row in rows.Values)
            {
                var id = ProductId(connector.Capability.Marketplace, row.ExternalProductId, row.ExternalVariantId);
                var observation = await db.PriceObservations.AsNoTracking().SingleOrDefaultAsync(
                    x => x.ProductId == id && x.SourceObservationId == row.SourceObservationId, ct);
                if (observation is not null && (observation.Price != row.Price || observation.Currency != row.Currency ||
                    observation.CapturedAt != row.CapturedAt || observation.InStock != row.InStock))
                    throw new ArgumentException("An existing observation ID cannot be rewritten.");
            }
            foreach (var group in rows.Values.GroupBy(x => (x.ExternalProductId, x.ExternalVariantId)))
            {
                var id = ProductId(connector.Capability.Marketplace, group.Key.ExternalProductId, group.Key.ExternalVariantId);
                var product = await db.Products.Include(x => x.Observations).SingleOrDefaultAsync(x => x.Id == id, ct);
                var newest = group.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId).First();
                if (product is null)
                {
                    product = new Product
                    {
                        Id = id, Marketplace = connector.Capability.Marketplace,
                        ExternalProductId = newest.ExternalProductId, ExternalVariantId = newest.ExternalVariantId,
                        Title = newest.Title, VariantLabel = newest.VariantLabel, Category = newest.Category,
                        IsSample = connector.Capability.Access == ConnectorAccess.SampleFeed, CreatedAt = clock.GetUtcNow()
                    };
                    db.Products.Add(product);
                    addedProducts++;
                }
                else if (product.IsSample != (connector.Capability.Access == ConnectorAccess.SampleFeed))
                    throw new ArgumentException("Sample and live feeds must not share a listing identity.");
                var latestStored = product.Observations.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId).FirstOrDefault();
                if (latestStored is null || newest.CapturedAt >= latestStored.CapturedAt)
                {
                    product.Title = newest.Title; product.VariantLabel = newest.VariantLabel; product.Category = newest.Category;
                    product.ProductUrl = newest.ProductUrl; product.ImageUrl = newest.ImageUrl;
                    product.ComparableVariantKey = newest.ComparableVariantKey; product.OriginalPrice = newest.OriginalPrice;
                    product.AffiliateUrl = newest.AffiliateUrl; product.DealEndAt = newest.DealEndAt;
                }
                foreach (var row in group)
                {
                    if (product.Observations.Any(x => x.SourceObservationId == row.SourceObservationId)) { skipped++; continue; }
                    var observation = new PriceObservation
                    {
                        Id = StableId($"observation|{id}|{row.SourceObservationId}"), ProductId = id,
                        Price = row.Price, Currency = row.Currency, CapturedAt = row.CapturedAt,
                        SourceObservationId = row.SourceObservationId, InStock = row.InStock
                    };
                    product.Observations.Add(observation);
                    db.PriceObservations.Add(observation);
                    addedObservations++;
                    changedProducts.Add(id);
                }
            }
            await db.SaveChangesAsync(ct);
            foreach (var productId in changedProducts) await alerts.EvaluateProductAsync(productId, ct);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new ImportResult(addedProducts, addedObservations, skipped);
        }
        finally { mutationLock.Semaphore.Release(); }
    }

    private void Validate(FeedProduct row)
    {
        DomainRules.Required(row.ExternalProductId, 100); DomainRules.Required(row.ExternalVariantId, 100);
        DomainRules.Required(row.SourceObservationId, 150); DomainRules.Required(row.Title, 300);
        DomainRules.Required(row.VariantLabel, 150); DomainRules.Required(row.Category, 100);
        DomainRules.Money(row.Price, row.Currency); DomainRules.Utc(row.CapturedAt);
        if (row.CapturedAt > clock.GetUtcNow()) throw new ArgumentException("Future price observations are not accepted.");
        DomainRules.OptionalUrl(row.ProductUrl); DomainRules.OptionalUrl(row.ImageUrl); DomainRules.OptionalUrl(row.AffiliateUrl);
        if (row.OriginalPrice is { } original) DomainRules.Money(original, "THB");
        if (row.DealEndAt is { } end) DomainRules.Utc(end);
        if (row.ComparableVariantKey is { } comparable) DomainRules.Required(comparable, 150);
    }

    public static Guid ProductId(Marketplace marketplace, string product, string variant) => StableId($"product|{marketplace}|{product.Length}:{product}|{variant}");
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
}
