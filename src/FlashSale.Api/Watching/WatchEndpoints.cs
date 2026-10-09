using System.Security.Claims;
using FlashSale.Api.Catalog;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Watching;

public sealed record WatchRequest(Guid ProductId, decimal? TargetPrice);
public sealed record WatchDto(Guid Id, ProductDto Product, decimal? TargetPrice, bool AlertEnabled);
public sealed record AlertDto(Guid Id, Guid WatchlistEntryId, Guid ProductId, string Title, string VariantLabel,
    decimal Price, decimal TargetPrice, string Currency, DateTimeOffset CapturedAt, DateTimeOffset CreatedAt,
    bool IsSample, string Channel);

public static class WatchEndpoints
{
    public static void MapWatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization("user");
        group.MapGet("/watchlist", async (ClaimsPrincipal principal, CurrentUser users, FlashSaleDbContext db, CancellationToken ct) =>
        {
            var user = await users.GetOrCreateAsync(principal, ct);
            var entries = await db.WatchlistEntries.AsNoTracking().Where(x => x.UserId == user.Id).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
            var ids = entries.Select(x => x.ProductId).ToArray();
            var products = await ProductEndpoints.Project(db.Products.AsNoTracking().Where(x => ids.Contains(x.Id))).ToDictionaryAsync(x => x.Id, ct);
            var entryIds = entries.Select(x => x.Id).ToArray();
            var alerts = await db.PriceAlerts.AsNoTracking().Where(x => entryIds.Contains(x.WatchlistEntryId)).ToDictionaryAsync(x => x.WatchlistEntryId, ct);
            return Results.Ok(new { Items = entries.Select(entry => new WatchDto(entry.Id, products[entry.ProductId],
                alerts.GetValueOrDefault(entry.Id)?.Enabled == true ? alerts[entry.Id].TargetPrice : null,
                alerts.GetValueOrDefault(entry.Id)?.Enabled ?? false)) });
        });
        group.MapPost("/watchlist", Upsert);
        group.MapDelete("/watchlist/{id:guid}", async (Guid id, ClaimsPrincipal principal, CurrentUser users,
            FlashSaleDbContext db, DataMutationLock mutationLock, CancellationToken ct) =>
        {
            var user = await users.GetOrCreateAsync(principal, ct);
            await mutationLock.Semaphore.WaitAsync(ct);
            try
            {
                var entry = await db.WatchlistEntries.SingleOrDefaultAsync(x => x.Id == id && x.UserId == user.Id, ct);
                if (entry is null) return Results.NotFound();
                // Load dependants for the demo provider, which does not enforce database cascades.
                var alert = await db.PriceAlerts.SingleOrDefaultAsync(x => x.WatchlistEntryId == id, ct);
                if (alert is not null)
                {
                    db.AlertEvents.RemoveRange(await db.AlertEvents.Where(x => x.PriceAlertId == alert.Id).ToListAsync(ct));
                    db.PriceAlerts.Remove(alert);
                }
                db.WatchlistEntries.Remove(entry);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
            finally { mutationLock.Semaphore.Release(); }
        });
        group.MapGet("/alerts", async (ClaimsPrincipal principal, CurrentUser users, FlashSaleDbContext db, CancellationToken ct) =>
        {
            var user = await users.GetOrCreateAsync(principal, ct);
            var events = await db.AlertEvents.AsNoTracking().Where(x => x.PriceAlert.WatchlistEntry.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(100)
                .Select(x => new AlertDto(x.Id, x.PriceAlert.WatchlistEntryId, x.Observation.ProductId,
                    x.Observation.Product.Title, x.Observation.Product.VariantLabel, x.Price, x.TargetPrice,
                    "THB", x.Observation.CapturedAt, x.CreatedAt, x.Observation.Product.IsSample, "inApp")).ToListAsync(ct);
            return Results.Ok(new { Items = events });
        });
    }

    private static async Task<IResult> Upsert(WatchRequest request, ClaimsPrincipal principal, CurrentUser users,
        FlashSaleDbContext db, DataMutationLock mutationLock, AlertEvaluator evaluator, CancellationToken ct)
    {
        if (request.TargetPrice is { } target)
        {
            try { DomainRules.Money(target, "THB"); }
            catch (ArgumentException exception) { return Results.Problem(title: "Invalid target price", detail: exception.Message, statusCode: 400); }
        }
        var product = await ProductEndpoints.Project(db.Products.Where(x => x.Id == request.ProductId)).SingleOrDefaultAsync(ct);
        if (product is null) return Results.NotFound();
        var user = await users.GetOrCreateAsync(principal, ct);
        await mutationLock.Semaphore.WaitAsync(ct);
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            var entry = await db.WatchlistEntries.SingleOrDefaultAsync(x => x.UserId == user.Id && x.ProductId == request.ProductId, ct);
            if (entry is null)
            {
                entry = new WatchlistEntry { UserId = user.Id, ProductId = request.ProductId };
                db.WatchlistEntries.Add(entry);
            }
            var alert = await db.PriceAlerts.SingleOrDefaultAsync(x => x.WatchlistEntryId == entry.Id, ct);
            if (request.TargetPrice is { } price)
            {
                if (alert is null)
                {
                    alert = new PriceAlert { WatchlistEntryId = entry.Id, TargetPrice = price };
                    db.PriceAlerts.Add(alert);
                }
                else if (alert.TargetPrice != price || !alert.Enabled)
                {
                    alert.TargetPrice = price; alert.Enabled = true;
                    alert.LastObservedPrice = null; alert.LastEvaluatedObservationId = null;
                }
                await db.SaveChangesAsync(ct);
                var current = await db.PriceObservations.Where(x => x.ProductId == entry.ProductId)
                    .OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId).FirstOrDefaultAsync(ct);
                if (current is not null) await evaluator.EvaluateAsync(alert, current, ct);
            }
            else if (alert is not null) alert.Enabled = false;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(new WatchDto(entry.Id, product, alert?.Enabled == true ? alert.TargetPrice : null, alert?.Enabled ?? false));
        }
        finally { mutationLock.Semaphore.Release(); }
    }
}
