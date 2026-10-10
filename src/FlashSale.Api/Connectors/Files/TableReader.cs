using System.Text;
using System.Text.Json;

namespace FlashSale.Api.Connectors.Files;

public sealed record Table(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

/// <summary>Reads CSV (RFC 4180 quoting) and JSON exports into rows keyed by their original header names.</summary>
public static class TableReader
{
    public static Table ReadCsv(string text, string? delimiter = null)
    {
        text = text.TrimStart('﻿');
        var separator = string.IsNullOrEmpty(delimiter) ? DetectDelimiter(text) : delimiter[0];
        var records = new List<List<string>>();
        var field = new StringBuilder();
        var record = new List<string>();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0) quoted = true;
            else if (c == separator) { record.Add(field.ToString()); field.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                record.Add(field.ToString()); field.Clear();
                if (record.Any(x => x.Length > 0)) records.Add(record);
                record = [];
            }
            else field.Append(c);
        }
        record.Add(field.ToString());
        if (record.Any(x => x.Length > 0)) records.Add(record);
        if (records.Count == 0) return new Table([], []);

        var headers = records[0].Select(h => h.Trim()).ToList();
        var rows = records.Skip(1).Select(values =>
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
                if (headers[i].Length > 0) row[headers[i]] = i < values.Count ? values[i].Trim() : null;
            return (IReadOnlyDictionary<string, string?>)row;
        }).ToList();
        return new Table(headers.Where(h => h.Length > 0).ToList(), rows);
    }

    /// <summary>An array of objects, or an object holding one (found wherever it sits); also JSON Lines.</summary>
    public static Table ReadJson(string text)
    {
        text = text.TrimStart('﻿').Trim();
        var objects = new List<JsonElement>();
        if (text.Length > 0 && (text[0] == '{' || text[0] == '[') && TryParse(text, out var root))
        {
            if (FindObjectArray(root) is { } found) objects.AddRange(found.EnumerateArray());
            else if (root.ValueKind == JsonValueKind.Object) objects.Add(root);
        }
        else
        {
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (TryParse(line, out var item) && item.ValueKind == JsonValueKind.Object) objects.Add(item);
        }
        var headers = new List<string>();
        var rows = objects.Select(item =>
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                if (!headers.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) headers.Add(property.Name);
                row[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.GetRawText(),
                    JsonValueKind.Array when property.Value.GetArrayLength() > 0 && property.Value[0].ValueKind == JsonValueKind.String => property.Value[0].GetString(),
                    _ => null,
                };
            }
            return (IReadOnlyDictionary<string, string?>)row;
        }).ToList();
        return new Table(headers, rows);
    }

    private static char DetectDelimiter(string text)
    {
        var header = text.Split('\n', 2)[0];
        var best = new[] { ',', ';', '\t', '|' }.MaxBy(d => header.Count(c => c == d));
        return header.Contains(best) ? best : ',';
    }

    private static bool TryParse(string text, out JsonElement element)
    {
        try { element = JsonDocument.Parse(text).RootElement.Clone(); return true; }
        catch (JsonException) { element = default; return false; }
    }

    private static JsonElement? FindObjectArray(JsonElement node, int depth = 0)
    {
        if (depth > 6) return null;
        if (node.ValueKind == JsonValueKind.Array && node.GetArrayLength() > 0 && node[0].ValueKind == JsonValueKind.Object) return node;
        if (node.ValueKind == JsonValueKind.Object)
            foreach (var property in node.EnumerateObject())
                if (FindObjectArray(property.Value, depth + 1) is { } found) return found;
        return null;
    }
}
