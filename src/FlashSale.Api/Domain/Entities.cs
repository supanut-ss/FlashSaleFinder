namespace FlashSale.Api.Domain;

public enum Marketplace { Lazada, Shopee, TikTok }

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Marketplace Marketplace { get; set; }
    public required string ExternalProductId { get; set; }
    public required string ExternalVariantId { get; set; }
    public required string Title { get; set; }
    public required string VariantLabel { get; set; }
    public required string Category { get; set; }
    public string? ProductUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? ComparableVariantKey { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? AffiliateUrl { get; set; }
    public DateTimeOffset? DealEndAt { get; set; }
    public bool IsSample { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<PriceObservation> Observations { get; set; } = [];
}

public sealed class PriceObservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTimeOffset CapturedAt { get; set; }
    public required string SourceObservationId { get; set; }
    public bool InStock { get; set; } = true;
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Issuer { get; set; }
    public required string Subject { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WatchlistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PriceAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WatchlistEntryId { get; set; }
    public WatchlistEntry WatchlistEntry { get; set; } = null!;
    public decimal TargetPrice { get; set; }
    public string Currency { get; set; } = "THB";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastTriggeredAt { get; set; }
    public decimal? LastObservedPrice { get; set; }
    public Guid? LastEvaluatedObservationId { get; set; }
}

public sealed class AlertEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PriceAlertId { get; set; }
    public PriceAlert PriceAlert { get; set; } = null!;
    public Guid ObservationId { get; set; }
    public PriceObservation Observation { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal TargetPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class DomainRules
{
    public static void Money(decimal amount, string currency)
    {
        if (amount < 0 || amount > 9999999999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("THB amounts must be non-negative, within numeric(12,2), and have at most two decimal places.");
        if (currency != "THB") throw new ArgumentException("Only THB is supported.");
    }

    public static void Utc(DateTimeOffset timestamp)
    {
        if (timestamp.Offset != TimeSpan.Zero || timestamp == default)
            throw new ArgumentException("Captured timestamps must be non-default UTC values.");
    }

    public static void Required(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentException($"A non-empty value of at most {maxLength} characters is required.");
    }

    public static void OptionalUrl(string? value)
    {
        if (value is not null && (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new ArgumentException("Product and image URLs must be absolute HTTPS URLs.");
    }
}
