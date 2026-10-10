using System.Runtime.CompilerServices;
using FlashSale.Api.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlashSale.Api.Connectors.Lazada;

/// <summary>
/// Reads the affiliate product feed and exposes it as price observations. The API returns only the current offer price,
/// so price history builds up from repeated imports; the tracking link is Lazada's own and is never constructed locally.
/// </summary>
public sealed class LazadaAffiliateConnector(LazadaAffiliateClient client, IOptions<LazadaAffiliateOptions> options,
    TimeProvider clock, ILogger<LazadaAffiliateConnector> log) : IMarketplaceConnector
{
    private const int ProductIdBatch = 20;
    private readonly LazadaAffiliateOptions settings = options.Value;

    public ConnectorCapability Capability => new(Marketplace.Lazada, ConnectorAccess.OfficialApi, true,
        "Lazada Affiliate Open API product feed.");

    public async IAsyncEnumerable<FeedProduct> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!settings.IsConfigured) throw new InvalidOperationException("Lazada affiliate credentials are not configured.");
        var now = clock.GetUtcNow();
        var capturedAt = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var seen = new HashSet<string>();
        var links = 0;
        var skipped = new Dictionary<string, int>();

        foreach (var (category, productIds) in Queries())
        {
            for (var page = 1; page <= Math.Max(1, settings.MaxPages); page++)
            {
                var response = await client.GetProductFeedAsync(page, settings.PageSize, category, productIds, ct);
                var products = LazadaFeedParser.FindProducts(response);
                if (products.Count == 0)
                {
                    LazadaFeedParser.ThrowIfError(response);
                    break;
                }
                foreach (var item in products)
                {
                    string? link = null;
                    var id = item.TryGetProperty("productId", out var idNode) ? idNode.ToString() : null;
                    if (id is null || !seen.Add(id)) continue;
                    if (links < settings.MaxTrackingLinks && long.TryParse(id, out var numericId))
                    {
                        links++;
                        try { link = LazadaFeedParser.FindTrackingLink(await client.GetTrackingLinkAsync(numericId, ct)); }
                        catch (LazadaApiException e) { log.LogWarning("Tracking link for {Product} unavailable: {Reason}", id, e.Message); }
                    }
                    var row = LazadaFeedParser.Map(item, capturedAt, link, out var reason);
                    if (row is null) { skipped[reason!] = skipped.GetValueOrDefault(reason!) + 1; continue; }
                    yield return row;
                }
                if (products.Count < settings.PageSize) break;
            }
        }
        foreach (var (reason, count) in skipped) log.LogInformation("Skipped {Count} Lazada feed items: {Reason}", count, reason);
    }

    private IEnumerable<(long? Category, IReadOnlyCollection<long>? ProductIds)> Queries()
    {
        if (settings.ProductIds.Length > 0)
        {
            foreach (var batch in settings.ProductIds.Chunk(ProductIdBatch)) yield return (null, batch);
        }
        else if (settings.CategoryIds.Length > 0)
        {
            foreach (var category in settings.CategoryIds) yield return (category, null);
        }
        else yield return (null, null);
    }
}
