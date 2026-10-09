using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Catalog;

public sealed record ProductDto
{
    public Guid Id { get; init; }
    public string Marketplace { get; init; } = "";
    public string Title { get; init; } = "";
    public string VariantLabel { get; init; } = "";
    public string Category { get; init; } = "";
    public string ExternalProductId { get; init; } = "";
    public string ExternalVariantId { get; init; } = "";
    public string? ProductUrl { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsSample { get; init; }
    public decimal? CurrentPrice { get; init; }
    public string Currency { get; init; } = "THB";
    public DateTimeOffset? CapturedAt { get; init; }
    public bool? InStock { get; init; }
    public decimal? OriginalPrice { get; init; }
    public string? AffiliateUrl { get; init; }
    public DateTimeOffset? DealEndAt { get; init; }
}
public sealed record PriceDto(decimal Price, string Currency, DateTimeOffset CapturedAt, bool InStock);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        app.MapGet("/api/products", Search);
        app.MapGet("/api/products/{id:guid}", async (Guid id, FlashSaleDbContext db, CancellationToken ct) =>
        {
            var product = await Project(db.Products.Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });
        app.MapGet("/api/products/{id:guid}/prices", async (Guid id, int? page, int? pageSize, FlashSaleDbContext db, CancellationToken ct) =>
        {
            var p = page ?? 1;
            var size = pageSize ?? 100;
            if (p is < 1 or > 100000 || size is < 1 or > 200) return Invalid("Use page 1–100000 and pageSize 1–200.");
            if (!await db.Products.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
            var query = db.PriceObservations.AsNoTracking().Where(x => x.ProductId == id);
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.SourceObservationId)
                .Skip((p - 1) * size).Take(size).Select(x => new PriceDto(x.Price, x.Currency, x.CapturedAt, x.InStock)).ToListAsync(ct);
            return Results.Ok(new PagedResult<PriceDto>(items, total, p, size));
        });
    }

    private static async Task<IResult> Search(string? q, string? marketplace, string? category, decimal? minPrice,
        decimal? maxPrice, string? sort, int? page, int? pageSize, FlashSaleDbContext db, CancellationToken ct)
    {
        var p = page ?? 1;
        var size = pageSize ?? 20;
        sort ??= "newest";
        if (p is < 1 or > 100000 || size is < 1 or > 100 || q?.Length > 200 || category?.Length > 100)
            return Invalid("Use page 1–100000, pageSize 1–100, q up to 200 characters and category up to 100 characters.");
        if (minPrice < 0 || maxPrice < 0 || minPrice > maxPrice) return Invalid("Invalid price range.");
        if (sort is not ("priceAsc" or "priceDesc" or "newest")) return Invalid("Sort must be priceAsc, priceDesc or newest.");
        var products = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(marketplace))
        {
            if (!Enum.TryParse<Marketplace>(marketplace, true, out var market) || !Enum.IsDefined(market))
                return Invalid("Marketplace must be Lazada, Shopee or TikTok.");
            products = products.Where(x => x.Marketplace == market);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            products = products.Where(x => x.Title.ToLower().Contains(term) || x.VariantLabel.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(category)) products = products.Where(x => x.Category == category);
        var projected = Project(products);
        if (minPrice is { } min) projected = projected.Where(x => x.CurrentPrice >= min);
        if (maxPrice is { } max) projected = projected.Where(x => x.CurrentPrice <= max);
        var total = await projected.CountAsync(ct);
        var sorted = sort switch
        {
            "priceAsc" => projected.OrderBy(x => x.CurrentPrice == null).ThenBy(x => x.CurrentPrice).ThenBy(x => x.Id),
            "priceDesc" => projected.OrderBy(x => x.CurrentPrice == null).ThenByDescending(x => x.CurrentPrice).ThenBy(x => x.Id),
            _ => projected.OrderByDescending(x => x.CapturedAt).ThenBy(x => x.Id)
        };
        return Results.Ok(new PagedResult<ProductDto>(await sorted.Skip((p - 1) * size).Take(size).ToListAsync(ct), total, p, size));
    }

    public static IQueryable<ProductDto> Project(IQueryable<Product> query) => query.Select(x => new ProductDto
    {
        Id = x.Id, Marketplace = x.Marketplace.ToString(), Title = x.Title,
        VariantLabel = x.VariantLabel, Category = x.Category, ExternalProductId = x.ExternalProductId,
        ExternalVariantId = x.ExternalVariantId, ProductUrl = x.ProductUrl, ImageUrl = x.ImageUrl, IsSample = x.IsSample,
        OriginalPrice = x.OriginalPrice, AffiliateUrl = x.AffiliateUrl, DealEndAt = x.DealEndAt,
        CurrentPrice = x.Observations.OrderByDescending(o => o.CapturedAt).ThenBy(o => o.SourceObservationId).Select(o => (decimal?)o.Price).FirstOrDefault(),
        CapturedAt = x.Observations.OrderByDescending(o => o.CapturedAt).ThenBy(o => o.SourceObservationId).Select(o => (DateTimeOffset?)o.CapturedAt).FirstOrDefault(),
        InStock = x.Observations.OrderByDescending(o => o.CapturedAt).ThenBy(o => o.SourceObservationId).Select(o => (bool?)o.InStock).FirstOrDefault()
    });

    private static IResult Invalid(string detail) => Results.Problem(detail: detail, statusCode: 400, title: "Invalid query");
}
