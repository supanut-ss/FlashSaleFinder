using Microsoft.Extensions.Options;

namespace FlashSale.Api.Connectors.Lazada;

/// <summary>Imports the Lazada affiliate feed on a schedule. Failures are logged and retried at the next interval.</summary>
public sealed class LazadaIngestService(IServiceScopeFactory scopes, IOptions<LazadaAffiliateOptions> options,
    ILogger<LazadaIngestService> log) : BackgroundService
{
    // Rate limits are not published in the portal, so never poll faster than every 15 minutes.
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(MinimumInterval.TotalMinutes, options.Value.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);
        try
        {
            do await RunOnceAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<FeedImporter>()
                .ImportAsync(scope.ServiceProvider.GetRequiredService<LazadaAffiliateConnector>(), ct);
            log.LogInformation("Lazada import finished: {Products} new products, {Added} new observations, {Skipped} already stored.",
                result.ProductsAdded, result.ObservationsAdded, result.ObservationsSkipped);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError(e, "Lazada import failed; will retry at the next interval.");
        }
    }
}
