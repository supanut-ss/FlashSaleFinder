namespace FlashSale.Api.Connectors.Files;

/// <summary>
/// A feed read from files exported by hand from a marketplace portal (configured per marketplace under
/// <c>FileFeeds:&lt;Marketplace&gt;</c>, e.g. <c>FileFeeds__Shopee__Path</c>).
/// </summary>
public sealed class FileFeedOptions
{
    /// <summary>A .csv or .json file, or a folder whose .csv/.json files are all read (oldest first).</summary>
    public string Path { get; set; } = "";

    /// <summary>Field delimiter for CSV; detected from the header line when empty.</summary>
    public string? Delimiter { get; set; }

    /// <summary>
    /// Overrides for column detection: field name (ProductId, ShopId, Title, Price, OriginalPrice, Category, Url, ImageUrl,
    /// AffiliateUrl, Stock, InStock, Currency, CapturedAt) to the exact header used in the file.
    /// </summary>
    public Dictionary<string, string> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int IntervalMinutes { get; set; } = 60;
    public long MaxFileBytes { get; set; } = 100 * 1024 * 1024;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Path);
}
