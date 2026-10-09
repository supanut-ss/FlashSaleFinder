using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Watching;

public sealed class AlertEvaluator(FlashSaleDbContext db, TimeProvider clock)
{
    public async Task EvaluateProductAsync(Guid productId, CancellationToken ct)
    {
        var current = await db.PriceObservations.Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId).FirstOrDefaultAsync(ct);
        if (current is null) return;
        var alerts = await db.PriceAlerts.Where(x => x.Enabled && x.WatchlistEntry.ProductId == productId).ToListAsync(ct);
        foreach (var alert in alerts) await EvaluateAsync(alert, current, ct);
    }

    public async Task EvaluateAsync(PriceAlert alert, PriceObservation current, CancellationToken ct)
    {
        if (!alert.Enabled || alert.LastEvaluatedObservationId == current.Id) return;
        var now = clock.GetUtcNow();
        var fresh = current.CapturedAt <= now && now - current.CapturedAt <= TimeSpan.FromHours(24);
        if (!fresh || !current.InStock)
        {
            alert.LastEvaluatedObservationId = current.Id;
            return;
        }
        var crosses = current.Price <= alert.TargetPrice && (alert.LastObservedPrice is null || alert.LastObservedPrice > alert.TargetPrice);
        if (crosses && !await db.AlertEvents.AnyAsync(x => x.PriceAlertId == alert.Id && x.ObservationId == current.Id, ct))
        {
            db.AlertEvents.Add(new AlertEvent
            {
                PriceAlertId = alert.Id, ObservationId = current.Id, Price = current.Price,
                TargetPrice = alert.TargetPrice, CreatedAt = now
            });
            alert.LastTriggeredAt = now;
        }
        alert.LastObservedPrice = current.Price;
        alert.LastEvaluatedObservationId = current.Id;
    }
}
