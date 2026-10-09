using FlashSale.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Data;

public sealed class FlashSaleDbContext(DbContextOptions<FlashSaleDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceObservation> PriceObservations => Set<PriceObservation>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<WatchlistEntry> WatchlistEntries => Set<WatchlistEntry>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Product>(b =>
        {
            b.Property(x => x.Marketplace).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.ExternalProductId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            b.Property(x => x.ExternalVariantId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            b.Property(x => x.Title).HasMaxLength(300);
            b.Property(x => x.VariantLabel).HasMaxLength(150);
            b.Property(x => x.Category).HasMaxLength(100);
            b.Property(x => x.ComparableVariantKey).HasMaxLength(150);
            b.Property(x => x.ProductUrl).HasMaxLength(2048);
            b.Property(x => x.ImageUrl).HasMaxLength(2048);
            b.Property(x => x.OriginalPrice).HasPrecision(12, 2);
            b.Property(x => x.AffiliateUrl).HasMaxLength(2048);
            b.HasIndex(x => new { x.Marketplace, x.ExternalProductId, x.ExternalVariantId }).IsUnique();
            b.HasIndex(x => x.Category);
            b.ToTable("Products", t =>
            {
                t.HasCheckConstraint("CK_Products_Marketplace", "Marketplace IN ('Lazada', 'Shopee', 'TikTok')");
                t.HasCheckConstraint("CK_Products_OriginalPrice", "OriginalPrice IS NULL OR OriginalPrice >= 0");
            });
        });
        model.Entity<PriceObservation>(b =>
        {
            b.Property(x => x.Price).HasPrecision(12, 2);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.SourceObservationId).HasMaxLength(150).UseCollation("utf8mb4_bin");
            b.HasOne(x => x.Product).WithMany(x => x.Observations).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ProductId, x.SourceObservationId }).IsUnique();
            b.HasIndex(x => new { x.ProductId, x.CapturedAt });
            b.ToTable("PriceObservations", t =>
            {
                t.HasCheckConstraint("CK_Observations_Price", "Price >= 0");
                t.HasCheckConstraint("CK_Observations_Currency", "Currency = 'THB'");
            });
        });
        model.Entity<AlertEvent>(b =>
        {
            b.Property(x => x.Price).HasPrecision(12, 2);
            b.Property(x => x.TargetPrice).HasPrecision(12, 2);
            b.HasOne(x => x.PriceAlert).WithMany().HasForeignKey(x => x.PriceAlertId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Observation).WithMany().HasForeignKey(x => x.ObservationId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.PriceAlertId, x.ObservationId }).IsUnique();
            b.ToTable("AlertEvents", t =>
            {
                t.HasCheckConstraint("CK_AlertEvents_Price", "Price >= 0");
                t.HasCheckConstraint("CK_AlertEvents_TargetPrice", "TargetPrice >= 0");
            });
        });
        model.Entity<AppUser>(b =>
        {
            b.Property(x => x.Issuer).HasMaxLength(300).UseCollation("utf8mb4_bin");
            b.Property(x => x.Subject).HasMaxLength(200).UseCollation("utf8mb4_bin");
            b.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        });
        model.Entity<WatchlistEntry>(b =>
        {
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        });
        model.Entity<PriceAlert>(b =>
        {
            b.Property(x => x.TargetPrice).HasPrecision(12, 2);
            b.Property(x => x.LastObservedPrice).HasPrecision(12, 2);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.HasOne(x => x.WatchlistEntry).WithOne().HasForeignKey<PriceAlert>(x => x.WatchlistEntryId).OnDelete(DeleteBehavior.Cascade);
            b.ToTable("PriceAlerts", t =>
            {
                t.HasCheckConstraint("CK_Alerts_Price", "TargetPrice >= 0");
                t.HasCheckConstraint("CK_Alerts_Currency", "Currency = 'THB'");
            });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateChanges()
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            switch (entry.Entity)
            {
                case Product product:
                    if (!Enum.IsDefined(product.Marketplace)) throw new ArgumentException("Unknown marketplace.");
                    DomainRules.Required(product.ExternalProductId, 100);
                    DomainRules.Required(product.ExternalVariantId, 100);
                    DomainRules.Required(product.Title, 300);
                    DomainRules.Required(product.VariantLabel, 150);
                    DomainRules.Required(product.Category, 100);
                    DomainRules.Utc(product.CreatedAt);
                    DomainRules.OptionalUrl(product.ProductUrl);
                    DomainRules.OptionalUrl(product.ImageUrl);
                    DomainRules.OptionalUrl(product.AffiliateUrl);
                    if (product.OriginalPrice is { } original) DomainRules.Money(original, "THB");
                    if (product.DealEndAt is { } end) DomainRules.Utc(end);
                    break;
                case PriceObservation observation:
                    DomainRules.Money(observation.Price, observation.Currency);
                    DomainRules.Utc(observation.CapturedAt);
                    DomainRules.Required(observation.SourceObservationId, 150);
                    break;
                case PriceAlert alert:
                    DomainRules.Money(alert.TargetPrice, alert.Currency);
                    if (alert.LastTriggeredAt is { } triggered) DomainRules.Utc(triggered);
                    if (alert.LastObservedPrice is { } observed) DomainRules.Money(observed, "THB");
                    break;
                case AlertEvent alertEvent:
                    DomainRules.Money(alertEvent.Price, "THB");
                    DomainRules.Money(alertEvent.TargetPrice, "THB");
                    DomainRules.Utc(alertEvent.CreatedAt);
                    break;
                case AppUser user:
                    DomainRules.Required(user.Issuer, 300);
                    DomainRules.Required(user.Subject, 200);
                    DomainRules.Utc(user.CreatedAt);
                    break;
                case WatchlistEntry watchlist:
                    DomainRules.Utc(watchlist.CreatedAt);
                    break;
            }
        }
    }
}
