using System.Globalization;
using System.Text.Json;

namespace FlashSale.Api.Connectors.Lazada;

/// <summary>
/// Reads feed responses. The field names come from the affiliate portal's "Get product feed" reference; the portal does not show the
/// response envelope, so the product list is located wherever it sits instead of assuming a wrapper. Run `--lazada-probe` once to
/// see a real response and tighten this if needed.
/// </summary>
public static class LazadaFeedParser
{
    private const string NoVariant = "default";
    private const string NoVariantLabel = "ไม่ระบุรุ่น";

    /// <summary>The first array of objects that carry a productId, searched depth-first (also inside JSON embedded as a string).</summary>
    public static IReadOnlyList<JsonElement> FindProducts(JsonElement node, int depth = 0)
    {
        if (depth > 8) return [];
        switch (node.ValueKind)
        {
            case JsonValueKind.Array:
                if (node.GetArrayLength() > 0 && node[0].ValueKind == JsonValueKind.Object && node[0].TryGetProperty("productId", out _))
                    return node.EnumerateArray().ToList();
                break;
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                    if (FindProducts(property.Value, depth + 1) is { Count: > 0 } found) return found;
                break;
            case JsonValueKind.String when node.GetString() is { Length: > 1 } text && (text[0] is '{' or '['):
                try { return FindProducts(JsonDocument.Parse(text).RootElement, depth + 1); } catch (JsonException) { }
                break;
        }
        return [];
    }

    /// <summary>Throws when the response is an API error rather than an (even empty) result.</summary>
    public static void ThrowIfError(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code)) return;
        var text = code.ValueKind == JsonValueKind.String ? code.GetString() : code.ToString();
        if (text is null or "0" or "00" or "") return;
        var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        throw new LazadaApiException($"Lazada returned an error ({text}){(message is null ? "" : ": " + message)}.");
    }

    public static string? FindTrackingLink(JsonElement node, int depth = 0)
    {
        if (depth > 8) return null;
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("trackingLink", out var link) && link.ValueKind == JsonValueKind.String) return link.GetString();
            foreach (var property in node.EnumerateObject())
                if (FindTrackingLink(property.Value, depth + 1) is { } found) return found;
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
                if (FindTrackingLink(item, depth + 1) is { } found) return found;
        }
        return null;
    }

    /// <summary>Maps one feed item; returns null (with a reason) for items the app must not store.</summary>
    public static FeedProduct? Map(JsonElement item, DateTimeOffset capturedAt, string? affiliateUrl, out string? skipReason)
    {
        skipReason = null;
        var productId = Text(item, "productId");
        var name = Text(item, "productName");
        if (productId is null || productId.Length > 100) { skipReason = "missing or oversized productId"; return null; }
        if (string.IsNullOrWhiteSpace(name)) { skipReason = "missing productName"; return null; }
        if (!item.TryGetProperty("discountPrice", out var priceNode) || Decimal(priceNode) is not { } price || price <= 0)
        { skipReason = "no usable discountPrice"; return null; }
        if (decimal.Round(price, 2) != price) { skipReason = "price has more than two decimals"; return null; }
        if (!string.Equals(Text(item, "currency"), "THB", StringComparison.OrdinalIgnoreCase))
        { skipReason = "currency is not THB"; return null; }

        var outOfStock = item.TryGetProperty("outOfStock", out var flag) && flag.ValueKind == JsonValueKind.True;
        var stockKnown = item.TryGetProperty("stock", out var stockNode) && Decimal(stockNode) is not null;
        var inStock = !outOfStock && (!stockKnown || Decimal(stockNode) > 0);
        var category = Text(item, "categoryL1") is { } id ? $"หมวด Lazada {id}" : "Lazada";
        var link = affiliateUrl is not null && Uri.TryCreate(affiliateUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" ? affiliateUrl : null;

        return new FeedProduct(productId, NoVariant, name.Length > 300 ? name[..300] : name, NoVariantLabel, category,
            null, FirstImage(item), null, price, "THB", capturedAt,
            $"lazada-aff-{productId}-{capturedAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}", inStock, null, link);
    }

    private static string? Text(JsonElement item, string name) => !item.TryGetProperty(name, out var value) ? null : value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null,
    };

    private static decimal? Decimal(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetDecimal(out var number) ? number : null,
        JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    private static string? FirstImage(JsonElement item)
    {
        if (!item.TryGetProperty("pictures", out var pictures)) return null;
        var first = pictures.ValueKind switch
        {
            JsonValueKind.Array when pictures.GetArrayLength() > 0 => pictures[0].ValueKind == JsonValueKind.String ? pictures[0].GetString() : null,
            JsonValueKind.String => pictures.GetString()?.Split(',', ';')[0].Trim(),
            _ => null,
        };
        return first is not null && first.Length <= 2048 && Uri.TryCreate(first, UriKind.Absolute, out var uri) && uri.Scheme == "https" ? first : null;
    }
}
