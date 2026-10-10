using System.Security.Cryptography;
using System.Text;

namespace FlashSale.Api.Connectors.Lazada;

/// <summary>
/// Request signature as described in the Lazada Open Platform documentation: HMAC-SHA256 over the API path followed by every
/// non-empty parameter as name+value in ordinal name order, keyed with the app secret, rendered as upper-case hex.
/// </summary>
public static class LazadaSigner
{
    public static string Sign(string apiPath, IReadOnlyDictionary<string, string> parameters, string appSecret)
    {
        var text = new StringBuilder(apiPath);
        foreach (var (name, value) in parameters
                     .Where(p => p.Key != "sign" && !string.IsNullOrEmpty(p.Value))
                     .OrderBy(p => p.Key, StringComparer.Ordinal))
            text.Append(name).Append(value);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
