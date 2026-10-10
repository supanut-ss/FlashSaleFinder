namespace FlashSale.Api.Connectors;

/// <summary>
/// Imports every configured <see cref="IMarketplaceSource"/> on its own schedule. A failing source is logged and retried at
/// its next interval without affecting the others.
/// </summary>
public sealed class MarketplaceIngestService(IServiceScopeFactory scopes, ILogger<MarketplaceIngestService> log) : BackgroundService
{
    // Rate limits are not published for most marketplaces, so never poll faster than every 15 minutes.
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<(string Name, TimeSpan Interval)> plan;
        using (var scope = scopes.CreateScope())
        {
            plan = scope.ServiceProvider.GetServices<IMarketplaceSource>()
                .Where(s => s.IsConfigured)
                .Select(s => (s.Name, s.Interval < MinimumInterval ? MinimumInterval : s.Interval))
                .ToList();
        }
        if (plan.Count == 0)
        {
            log.LogInformation("No marketplace feed is configured; nothing to import.");
            return;
        }
        await Task.WhenAll(plan.Select(p => RunLoopAsync(p.Name, p.Interval, stoppingToken)));
    }

    private async Task RunLoopAsync(string name, TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            do await RunOnceAsync(name, ct);
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    public async Task RunOnceAsync(string name, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var source = scope.ServiceProvider.GetServices<IMarketplaceSource>().Single(s => s.Name == name);
            var result = await scope.ServiceProvider.GetRequiredService<FeedImporter>().ImportAsync(source, ct);
            log.LogInformation("{Source} import finished: {Products} new products, {Added} new observations, {Skipped} already stored.",
                name, result.ProductsAdded, result.ObservationsAdded, result.ObservationsSkipped);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError(e, "{Source} import failed; will retry at the next interval.", name);
        }
    }
}
