using System.Runtime.CompilerServices;
using FlashSale.Api.Connectors;
using FlashSale.Api.Domain;

namespace FlashSale.Api.Demo;

public sealed class SampleFeed(TimeProvider clock) : IMarketplaceConnector
{
    public ConnectorCapability Capability => new(Marketplace.Lazada, ConnectorAccess.SampleFeed, true,
        "Synthetic local fixture; no marketplace connection.");

    public async IAsyncEnumerable<FeedProduct> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var today = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        foreach (var sample in Samples)
        {
            for (var day = sample.Days - 1; day >= 0; day--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var captured = today.AddDays(-day);
                var price = day == 0 ? sample.Current : sample.Baseline + (day % 7 - 3) * sample.Step;
                yield return new FeedProduct(sample.Id, sample.Variant, sample.Title, sample.Label, sample.Category,
                    null, null, null, price, "THB", captured, $"sample-v1-{captured:yyyyMMdd}", true,
                    sample.Original);
            }
        }
        await Task.CompletedTask;
    }

    private sealed record Sample(string Id, string Variant, string Title, string Label, string Category,
        decimal Baseline, decimal Current, decimal Original, decimal Step, int Days);

    private static readonly Sample[] Samples =
    [
        new("sample-headphones", "black", "หูฟังไร้สาย Wave Mini", "สีดำ · รุ่น Mini", "Electronics", 1290, 990, 1990, 10, 60),
        new("sample-coffee", "white", "เครื่องชงกาแฟ Morning Brew", "สีขาว · 1.2 ลิตร", "Home", 2490, 2390, 3490, 20, 90),
        new("sample-powerbank", "20k", "พาวเวอร์แบงก์ Pocket Charge", "20,000 mAh · USB-C", "Electronics", 690, 499, 990, 5, 45),
        new("sample-serum", "30ml", "เซรั่ม Hydrate Daily", "30 ml · สูตรผิวแห้ง", "Beauty", 590, 390, 890, 5, 35),
        new("sample-airfryer", "4l", "หม้อทอด Air Crisp", "4 ลิตร · สีครีม", "Home", 2290, 2590, 2990, 15, 60),
        new("sample-monitor", "24in", "จอภาพ Studio View", "24 นิ้ว · IPS · 100 Hz", "Electronics", 4990, 4290, 6990, 30, 90)
    ];
}
