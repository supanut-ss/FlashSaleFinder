using System.Globalization;
using System.Text.RegularExpressions;
using FlashSale.Api.Domain;

namespace FlashSale.Api.Connectors.Files;

public sealed class FileFeedException(string message) : Exception(message);

/// <summary>Which file header feeds which field.</summary>
public sealed record ColumnMap(IReadOnlyDictionary<string, string> Fields)
{
    public string? Get(string field) => Fields.GetValueOrDefault(field);
}

/// <summary>
/// Turns exported rows into price observations. Column names differ between portals and change over time, so headers are matched
/// by a list of known English and Thai names and can be overridden explicitly; <c>--feed-probe</c> shows what was matched.
/// </summary>
public static partial class FileFeedMapper
{
    public static readonly string[] Required = ["ProductId", "Title", "Price"];

    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ProductId"] = ["productid", "itemid", "id", "productcode", "รหัสสินค้า", "ไอดีสินค้า"],
        ["ShopId"] = ["shopid", "sellerid", "storeid", "รหัสร้านค้า", "ไอดีร้านค้า"],
        ["Title"] = ["title", "name", "productname", "itemname", "ชื่อสินค้า", "ชื่อ"],
        ["Price"] = ["price", "saleprice", "currentprice", "discountprice", "sellingprice", "offerprice", "ราคา", "ราคาขาย", "ราคาหลังลด", "ราคาปัจจุบัน"],
        ["OriginalPrice"] = ["originalprice", "listprice", "regularprice", "pricebeforediscount", "ราคาเดิม", "ราคาปกติ", "ราคาก่อนลด"],
        ["Category"] = ["category", "categoryname", "categoryl1", "หมวดหมู่", "หมวด", "หมวดหมู่สินค้า"],
        ["Url"] = ["url", "producturl", "itemurl", "productlink", "link", "ลิงก์สินค้า", "ลิงค์สินค้า"],
        ["ImageUrl"] = ["image", "imageurl", "picture", "thumbnail", "รูป", "รูปภาพ", "ลิงก์รูปภาพ"],
        ["AffiliateUrl"] = ["affiliatelink", "affiliateurl", "trackinglink", "promolink", "shortlink", "ลิงก์แอฟฟิลิเอต", "ลิงก์ติดตาม"],
        ["Stock"] = ["stock", "quantity", "inventory", "สต็อก", "จำนวนคงเหลือ"],
        ["InStock"] = ["instock", "available", "availability", "มีสินค้า"],
        ["Currency"] = ["currency", "สกุลเงิน"],
        ["CapturedAt"] = ["capturedat", "updatedat", "date", "timestamp", "datetime", "วันที่", "เวลา", "วันที่อัปเดต"],
    };

    // Thai vowels and tone marks are combining marks, not letters, so keep them; only separators and punctuation are dropped.
    public static string Normalize(string header) => new(header.Where(c => char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark).ToArray());

    public static ColumnMap Detect(IReadOnlyList<string> headers, IReadOnlyDictionary<string, string> overrides)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (field, names) in Aliases)
        {
            if (overrides.TryGetValue(field, out var wanted))
            {
                map[field] = headers.FirstOrDefault(h => string.Equals(h, wanted, StringComparison.OrdinalIgnoreCase))
                    ?? throw new FileFeedException($"The column \"{wanted}\" set for {field} is not in the file. Headers found: {string.Join(", ", headers)}.");
                continue;
            }
            var known = names.Select(n => Normalize(n).ToLowerInvariant()).ToHashSet();
            var match = headers.FirstOrDefault(h => known.Contains(Normalize(h).ToLowerInvariant()));
            if (match is not null) map[field] = match;
        }
        var missing = Required.Where(f => !map.ContainsKey(f)).ToList();
        if (missing.Count > 0)
            throw new FileFeedException($"Could not find a column for {string.Join(", ", missing)}. Headers found: {string.Join(", ", headers)}. " +
                $"Name the columns with FileFeeds__<Marketplace>__Columns__{missing[0]}=<header>.");
        return new ColumnMap(map);
    }

    /// <summary>Maps one row; returns null with a reason for rows the app must not store.</summary>
    public static FeedProduct? Map(IReadOnlyDictionary<string, string?> row, ColumnMap columns, Marketplace marketplace,
        DateTimeOffset fileTime, DateTimeOffset now, out string? reason)
    {
        reason = null;
        string? Cell(string field) => columns.Get(field) is { } header && row.TryGetValue(header, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        var productId = Cell("ProductId");
        var shopId = Cell("ShopId");
        var title = Cell("Title");
        if (productId is null) { reason = "missing product id"; return null; }
        if (title is null) { reason = "missing title"; return null; }
        var externalId = shopId is null ? productId : $"{shopId}.{productId}";
        if (externalId.Length > 100) { reason = "product id too long"; return null; }

        if (!TryPrice(Cell("Price"), out var price, out reason)) return null;
        if (Cell("Currency") is { } currency && !IsBaht(currency)) { reason = "currency is not THB"; return null; }

        DateTimeOffset capturedAt = fileTime;
        if (Cell("CapturedAt") is { } stamp && TryTimestamp(stamp, out var parsed)) capturedAt = parsed;
        capturedAt = new DateTimeOffset(capturedAt.UtcTicks - capturedAt.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        if (capturedAt > now) { reason = "timestamp is in the future"; return null; }

        decimal? original = TryPrice(Cell("OriginalPrice"), out var list, out _) && list > price ? list : null;
        var category = Cell("Category") is { Length: <= 100 } c ? c : marketplace.ToString();
        var observationId = $"file-{marketplace.ToString().ToLowerInvariant()}-{externalId}-{capturedAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";

        return new FeedProduct(externalId, "default", title.Length > 300 ? title[..300] : title, "ไม่ระบุรุ่น", category,
            HttpsUrl(Cell("Url")), HttpsUrl(Cell("ImageUrl")), null, price, "THB", capturedAt, observationId,
            StockState(Cell("InStock"), Cell("Stock")), original, HttpsUrl(Cell("AffiliateUrl")));
    }

    public static bool TryPrice(string? text, out decimal price, out string? reason)
    {
        price = 0;
        reason = null;
        if (string.IsNullOrWhiteSpace(text)) { reason = "no price"; return false; }
        var compact = StripCurrency().Replace(text, "").Replace(" ", "");
        // A decimal comma ("299,5") would be misread as thousands (2995), so refuse it rather than store a wrong price.
        if (DecimalComma().IsMatch(compact)) { reason = "ambiguous number format"; return false; }
        var cleaned = compact.Replace(",", "");
        if (cleaned.IndexOfAny(['-', '–', '~']) > 0) { reason = "price is a range"; return false; }
        if (!decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price) || price <= 0)
        { reason = "no usable price"; return false; }
        if (decimal.Round(price, 2) != price) { reason = "price has more than two decimals"; return false; }
        return true;
    }

    public static bool TryTimestamp(string text, out DateTimeOffset value)
    {
        // A timestamp with an offset or Z is taken as written; a bare one is Thailand local time, as the portals show it.
        if (HasOffset().IsMatch(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var withOffset))
        {
            value = withOffset.ToUniversalTime();
            return true;
        }
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            value = new DateTimeOffset(local, TimeSpan.FromHours(7)).ToUniversalTime();
            return true;
        }
        value = default;
        return false;
    }

    private static bool IsBaht(string value) => value.Trim().ToUpperInvariant() is "THB" or "฿" or "บาท";

    private static bool StockState(string? flag, string? stock)
    {
        if (flag is not null)
        {
            var text = flag.Trim().ToLowerInvariant();
            if (text is "true" or "yes" or "y" or "1" or "มี" or "มีสินค้า" or "in stock" or "instock") return true;
            if (text is "false" or "no" or "n" or "0" or "หมด" or "สินค้าหมด" or "out of stock" or "outofstock") return false;
        }
        return stock is null || !decimal.TryParse(stock.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var count) || count > 0;
    }

    private static string? HttpsUrl(string? value) =>
        value is { Length: <= 2048 } && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? value : null;

    [GeneratedRegex(@"^[^.]*,\d{1,2}$")]
    private static partial Regex DecimalComma();

    [GeneratedRegex("[฿]|THB|บาท", RegexOptions.IgnoreCase)]
    private static partial Regex StripCurrency();

    [GeneratedRegex(@"(Z|[+-]\d{2}:?\d{2})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HasOffset();
}
