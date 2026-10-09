using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Catalog;

public sealed record PriceWindow(int Days, int ObservedDays, decimal? AveragePrice, decimal? LowestPrice);
public sealed record DealInsight(Guid ProductId, bool IsSample, string Currency, decimal? CurrentPrice,
    DateTimeOffset? CapturedAt, bool IsFresh, int PriorDays, string Evidence, decimal? ProvisionalScore,
    IReadOnlyList<PriceWindow> Windows, decimal? RealDiscountPercent, string ScoreEvidence);

public static class DealInsights
{
    public static DealInsight Calculate(Product product, IEnumerable<PriceObservation> observations, DateTimeOffset now)
    {
        var valid = observations.Where(x => x.CapturedAt <= now).ToList();
        var current = valid.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId).FirstOrDefault();
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        // Each UTC day has one vote, independent of how many intraday samples were imported.
        var daily = valid.Where(x => x.CapturedAt < today && x.CapturedAt >= today.AddDays(-90))
            .GroupBy(x => x.CapturedAt.UtcDateTime.Date)
            .Select(group => new { Day = group.Key, Average = group.Average(x => x.Price), Low = group.Min(x => x.Price) }).ToList();
        var windows = new[] { 7, 30, 90 }.Select(days =>
        {
            var included = daily.Where(x => x.Day >= today.AddDays(-days).UtcDateTime.Date).ToList();
            return new PriceWindow(days, included.Count,
                included.Count == 0 ? null : decimal.Round(included.Average(x => x.Average), 2),
                included.Count == 0 ? null : included.Min(x => x.Low));
        }).ToArray();
        var fresh = current is not null && now - current.CapturedAt <= TimeSpan.FromHours(24);
        var baseline = windows.Single(x => x.Days == 30);
        var sufficient = baseline.ObservedDays >= 7 && baseline.AveragePrice > 0;
        var evidence = current is null ? "noHistory" : !fresh ? "stale" : !sufficient ? "insufficientHistory" :
            !current.InStock ? "outOfStock" : "provisional";
        decimal? discount = evidence == "provisional"
            ? decimal.Round((baseline.AveragePrice!.Value - current!.Price) / baseline.AveragePrice.Value * 100, 1)
            : null;
        return new DealInsight(product.Id, product.IsSample, "THB", current?.Price, current?.CapturedAt,
            fresh, baseline.ObservedDays, evidence, null, windows, discount, "insufficientSignals");
    }

    public static void MapInsightEndpoints(this WebApplication app) => app.MapGet("/api/products/{id:guid}/insights",
        async (Guid id, FlashSaleDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (product is null) return Results.NotFound();
            var from = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero).AddDays(-90);
            var observations = await db.PriceObservations.AsNoTracking().Where(x => x.ProductId == id && x.CapturedAt >= from).ToListAsync(ct);
            var latest = await db.PriceObservations.AsNoTracking().Where(x => x.ProductId == id)
                .OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId).FirstOrDefaultAsync(ct);
            if (latest is not null && observations.All(x => x.Id != latest.Id)) observations.Add(latest);
            return Results.Ok(Calculate(product, observations, clock.GetUtcNow()));
        });
}
