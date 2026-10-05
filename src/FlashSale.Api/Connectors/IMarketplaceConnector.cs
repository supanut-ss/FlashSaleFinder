using FlashSale.Api.Domain;

namespace FlashSale.Api.Connectors;

public enum ConnectorAccess { Unavailable, SampleFeed, AuthorizedFeed, OfficialApi }
public sealed record ConnectorCapability(Marketplace Marketplace, ConnectorAccess Access, bool SupportsHistory, string Status);
public sealed record FeedProduct(string ExternalProductId, string ExternalVariantId, string Title,
    string VariantLabel, string Category, string? ProductUrl, string? ImageUrl, string? ComparableVariantKey,
    decimal Price, string Currency, DateTimeOffset CapturedAt, string SourceObservationId, bool InStock);

public interface IMarketplaceConnector
{
    ConnectorCapability Capability { get; }
    IAsyncEnumerable<FeedProduct> ReadAsync(CancellationToken cancellationToken = default);
}

public static class MarketplaceCapabilities
{
    public static IEnumerable<ConnectorCapability> Unavailable => Enum.GetValues<Marketplace>()
        .Select(x => new ConnectorCapability(x, ConnectorAccess.Unavailable, false, "Official access has not been configured."));
}
