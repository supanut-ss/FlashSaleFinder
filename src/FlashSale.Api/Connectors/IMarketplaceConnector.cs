using FlashSale.Api.Domain;

namespace FlashSale.Api.Connectors;

public enum ConnectorAccess { Unavailable, SampleFeed, AuthorizedFeed, OfficialApi }
public sealed record ConnectorCapability(Marketplace Marketplace, ConnectorAccess Access, bool SupportsHistory, string Status);
public sealed record FeedProduct(string ExternalProductId, string ExternalVariantId, string Title,
    string VariantLabel, string Category, string? ProductUrl, string? ImageUrl, string? ComparableVariantKey,
    decimal Price, string Currency, DateTimeOffset CapturedAt, string SourceObservationId, bool InStock,
    decimal? OriginalPrice = null, string? AffiliateUrl = null, DateTimeOffset? DealEndAt = null);

public interface IMarketplaceConnector
{
    ConnectorCapability Capability { get; }
    IAsyncEnumerable<FeedProduct> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A live marketplace feed the scheduler can run. Register one per marketplace
/// (<c>services.AddScoped&lt;IMarketplaceSource, MyConnector&gt;()</c>); the importer, scheduler and /api/status pick it up.
/// </summary>
public interface IMarketplaceSource : IMarketplaceConnector
{
    /// <summary>Unique name of this feed, e.g. "Lazada affiliate API"; a marketplace may have several (API and files).</summary>
    string Name { get; }

    Marketplace Marketplace { get; }

    /// <summary>False until the credentials and settings the marketplace needs are present; unconfigured sources are never run.</summary>
    bool IsConfigured { get; }

    /// <summary>How often the scheduler imports this source.</summary>
    TimeSpan Interval { get; }
}

public static class MarketplaceCapabilities
{
    /// <summary>One line per marketplace saying what is missing, shown in /api/status and the app.</summary>
    public static string WaitingFor(Marketplace marketplace) => marketplace switch
    {
        Marketplace.Lazada => "Add the Lazada affiliate API key, secret and user token (docs/operations.md).",
        Marketplace.Shopee => "No connector yet. Needs approved Shopee affiliate API access and its reference (docs/marketplaces.md).",
        Marketplace.TikTok => "No connector yet. Needs approved TikTok Shop API access and its reference (docs/marketplaces.md).",
        _ => "Official access has not been configured.",
    };

    public static ConnectorCapability Unavailable(Marketplace marketplace) =>
        new(marketplace, ConnectorAccess.Unavailable, false, WaitingFor(marketplace));

    public static IEnumerable<ConnectorCapability> Unavailable() => Enum.GetValues<Marketplace>().Select(Unavailable);
}
