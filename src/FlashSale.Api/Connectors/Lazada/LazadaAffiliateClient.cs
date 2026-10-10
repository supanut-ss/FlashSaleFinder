using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FlashSale.Api.Connectors.Lazada;

public sealed class LazadaApiException(string message) : Exception(message);

/// <summary>Thin signed GET client for the two affiliate endpoints the app needs.</summary>
public sealed class LazadaAffiliateClient(HttpClient http, IOptions<LazadaAffiliateOptions> options, TimeProvider clock)
{
    public const string FeedPath = "/marketing/product/feed";
    public const string LinkPath = "/marketing/product/link";

    private readonly LazadaAffiliateOptions settings = options.Value;

    public Task<JsonElement> GetProductFeedAsync(int page, int limit, long? categoryId, IReadOnlyCollection<long>? productIds, CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["offerType"] = settings.OfferType.ToString(CultureInfo.InvariantCulture),
            ["userToken"] = settings.UserToken,
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["limit"] = limit.ToString(CultureInfo.InvariantCulture),
        };
        if (categoryId is { } category) query["categoryL1"] = category.ToString(CultureInfo.InvariantCulture);
        if (productIds is { Count: > 0 }) query["productIds"] = "[" + string.Join(",", productIds) + "]";
        return GetAsync(FeedPath, query, ct);
    }

    public Task<JsonElement> GetTrackingLinkAsync(long productId, CancellationToken ct) => GetAsync(LinkPath, new Dictionary<string, string>
    {
        ["userToken"] = settings.UserToken,
        ["productId"] = productId.ToString(CultureInfo.InvariantCulture),
    }, ct);

    private async Task<JsonElement> GetAsync(string path, Dictionary<string, string> business, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>(business)
        {
            ["app_key"] = settings.AppKey,
            ["timestamp"] = clock.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            ["sign_method"] = "sha256",
        };
        parameters["sign"] = LazadaSigner.Sign(path, parameters, settings.AppSecret);
        var query = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        using var response = await http.GetAsync($"{settings.BaseUrl.TrimEnd('/')}{path}?{query}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new LazadaApiException($"Lazada returned HTTP {(int)response.StatusCode} for {path}.");
        try { return JsonDocument.Parse(body).RootElement.Clone(); }
        catch (JsonException) { throw new LazadaApiException($"Lazada returned a non-JSON response for {path}."); }
    }
}
