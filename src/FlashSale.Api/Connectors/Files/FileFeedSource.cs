using System.Runtime.CompilerServices;
using FlashSale.Api.Domain;

namespace FlashSale.Api.Connectors.Files;

public sealed record FeedPreview(IReadOnlyList<string> Headers, ColumnMap Columns, int RowCount, IReadOnlyList<string> Lines);

/// <summary>
/// Reads price observations from files exported by hand from a marketplace's portal (affiliate or seller centre). The data comes
/// from the operator's own account, so no API credentials or terms of an API are involved. The file's modified time is the
/// capture time unless the file carries its own timestamp column; importing the same file twice stores nothing new.
/// </summary>
public sealed class FileFeedSource(Marketplace marketplace, FileFeedOptions options, TimeProvider clock, ILogger<FileFeedSource> log) : IMarketplaceSource
{
    private static readonly string[] Extensions = [".csv", ".json", ".jsonl"];

    public string Name => $"{marketplace} file feed";
    public Marketplace Marketplace => marketplace;
    public bool IsConfigured => options.IsConfigured;
    public TimeSpan Interval => TimeSpan.FromMinutes(options.IntervalMinutes);

    public ConnectorCapability Capability =>
        !IsConfigured ? MarketplaceCapabilities.Unavailable(marketplace)
        : File.Exists(options.Path) || Directory.Exists(options.Path)
            ? new(marketplace, ConnectorAccess.AuthorizedFeed, true, "Imported from files exported from the marketplace portal.")
            : new(marketplace, ConnectorAccess.Unavailable, false, "The configured feed file or folder was not found.");

    public IReadOnlyList<string> Files()
    {
        if (File.Exists(options.Path)) return [options.Path];
        if (!Directory.Exists(options.Path)) return [];
        return Directory.EnumerateFiles(options.Path)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(File.GetLastWriteTimeUtc).ThenBy(f => f, StringComparer.Ordinal).ToList();
    }

    public async IAsyncEnumerable<FeedProduct> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException($"{Name} is not configured.");
        var files = Files();
        if (files.Count == 0) log.LogWarning("{Source}: no .csv/.json files found at the configured path.", Name);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var table = await LoadAsync(file, ct);
            var columns = FileFeedMapper.Detect(table.Headers, options.Columns);
            var now = clock.GetUtcNow();
            var fileTime = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);
            if (fileTime > now) fileTime = now;
            var seen = new HashSet<string>();
            var skipped = new Dictionary<string, int>();
            foreach (var row in table.Rows)
            {
                var item = FileFeedMapper.Map(row, columns, marketplace, fileTime, now, out var reason);
                if (item is null) { skipped[reason!] = skipped.GetValueOrDefault(reason!) + 1; continue; }
                if (!seen.Add(item.ExternalProductId)) { skipped["duplicate product in the same file"] = skipped.GetValueOrDefault("duplicate product in the same file") + 1; continue; }
                yield return item;
            }
            foreach (var (reason, count) in skipped) log.LogInformation("{Source}: skipped {Count} rows in {File}: {Reason}", Name, count, Path.GetFileName(file), reason);
        }
    }

    /// <summary>What a file would import, without importing it: matched columns and the first rows as they would be stored.</summary>
    public async Task<FeedPreview> PreviewAsync(string file, int take, CancellationToken ct = default)
    {
        var table = await LoadAsync(file, ct);
        var columns = FileFeedMapper.Detect(table.Headers, options.Columns);
        var now = clock.GetUtcNow();
        var fileTime = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);
        var lines = new List<string>();
        foreach (var row in table.Rows.Take(take))
        {
            var item = FileFeedMapper.Map(row, columns, marketplace, fileTime > now ? now : fileTime, now, out var reason);
            lines.Add(item is null ? $"skipped: {reason}" : $"{item.ExternalProductId}  {item.Price:0.00} THB  {(item.InStock ? "in stock" : "out of stock")}  {item.Title}");
        }
        return new FeedPreview(table.Headers, columns, table.Rows.Count, lines);
    }

    private async Task<Table> LoadAsync(string file, CancellationToken ct)
    {
        var info = new FileInfo(file);
        if (info.Length > options.MaxFileBytes) throw new FileFeedException($"{info.Name} is larger than the {options.MaxFileBytes / 1024 / 1024} MB limit.");
        var text = await File.ReadAllTextAsync(file, ct);
        return Path.GetExtension(file).ToLowerInvariant() is ".json" or ".jsonl" ? TableReader.ReadJson(text) : TableReader.ReadCsv(text, options.Delimiter);
    }
}
