namespace FlashSale.Api.Connectors.Lazada;

/// <summary>Settings for the Lazada Affiliate Open API (Integration &gt; Open API in the affiliate portal).</summary>
public sealed class LazadaAffiliateOptions
{
    public string AppKey { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string UserToken { get; set; } = "";

    // The portal samples show the Singapore host; Thailand traffic is expected on the .co.th gateway. Confirm with the probe.
    public string BaseUrl { get; set; } = "https://api.lazada.co.th/rest";

    /// <summary>1 regular offers, 2 MM offers, 3 DM offers.</summary>
    public int OfferType { get; set; } = 1;

    /// <summary>Level-1 category ids to read; empty reads the feed unfiltered.</summary>
    public long[] CategoryIds { get; set; } = [];

    /// <summary>Specific product ids to track; takes precedence over categories.</summary>
    public long[] ProductIds { get; set; } = [];

    public int PageSize { get; set; } = 50;
    public int MaxPages { get; set; } = 2;

    /// <summary>How many products per run also get a tracking link (one extra API call each).</summary>
    public int MaxTrackingLinks { get; set; }

    /// <summary>Minutes between background imports when the API runs as a service.</summary>
    public int IntervalMinutes { get; set; } = 60;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(AppSecret) && !string.IsNullOrWhiteSpace(UserToken);
}
