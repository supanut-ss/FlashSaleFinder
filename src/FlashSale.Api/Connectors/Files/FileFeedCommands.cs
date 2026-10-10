using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Connectors.Files;

/// <summary>
/// Command-line helpers for file feeds that run without the web host:
///   --feed-probe &lt;Marketplace&gt; [file-or-folder]   shows the columns matched and the first rows each file would import
///   --feed-import &lt;Marketplace&gt;                    one import into the MariaDB named by ConnectionStrings:MariaDb
/// Settings come from environment variables, e.g. FileFeeds__Shopee__Path and FileFeeds__Shopee__Columns__Price.
/// </summary>
public static class FileFeedCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2 || !Enum.TryParse<Marketplace>(args[1], true, out var marketplace))
        {
            Console.Error.WriteLine($"Usage: {args[0]} <{string.Join("|", Enum.GetNames<Marketplace>())}>" + (args[0] == "--feed-probe" ? " [file-or-folder]" : ""));
            return 2;
        }
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddEnvironmentVariables().Build();
        var settings = config.GetSection($"FileFeeds:{marketplace}").Get<FileFeedOptions>() ?? new FileFeedOptions();
        if (args[0] == "--feed-probe" && args.Length > 2) settings.Path = args[2];
        if (!settings.IsConfigured)
        {
            Console.Error.WriteLine($"Set FileFeeds__{marketplace}__Path to an exported file or a folder of exports.");
            return 2;
        }
        using var loggers = LoggerFactory.Create(b => b.AddSimpleConsole());
        var source = new FileFeedSource(marketplace, settings, TimeProvider.System, loggers.CreateLogger<FileFeedSource>());
        try
        {
            return args[0] == "--feed-probe" ? await ProbeAsync(source) : await ImportAsync(source, config);
        }
        catch (FileFeedException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    private static async Task<int> ProbeAsync(FileFeedSource source)
    {
        var files = source.Files();
        if (files.Count == 0)
        {
            Console.Error.WriteLine("No .csv, .json or .jsonl file found at that path.");
            return 1;
        }
        foreach (var file in files)
        {
            var preview = await source.PreviewAsync(file, 5);
            Console.WriteLine($"{Path.GetFileName(file)}: {preview.RowCount} rows");
            Console.WriteLine($"  Headers: {string.Join(" | ", preview.Headers)}");
            Console.WriteLine($"  Matched: {string.Join(", ", preview.Columns.Fields.Select(f => $"{f.Key} = \"{f.Value}\""))}");
            foreach (var line in preview.Lines) Console.WriteLine($"  {line}");
        }
        return 0;
    }

    private static async Task<int> ImportAsync(FileFeedSource source, IConfiguration config)
    {
        var connection = config["ConnectionStrings:MariaDb"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            Console.Error.WriteLine("Set ConnectionStrings__MariaDb to the database that should receive the prices.");
            return 2;
        }
        await using var db = new FlashSaleDbContext(new DbContextOptionsBuilder<FlashSaleDbContext>()
            .UseMySql(connection, new MariaDbServerVersion(new Version(11, 4, 0))).Options);
        var clock = TimeProvider.System;
        var importer = new FeedImporter(db, new DataMutationLock(), clock, new AlertEvaluator(db, clock));
        var result = await importer.ImportAsync(source);
        Console.WriteLine($"Imported: {result.ProductsAdded} new products, {result.ObservationsAdded} new observations, {result.ObservationsSkipped} already stored.");
        return 0;
    }
}
