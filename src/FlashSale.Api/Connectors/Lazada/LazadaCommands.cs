using FlashSale.Api.Data;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlashSale.Api.Connectors.Lazada;

/// <summary>
/// Command-line helpers that run without starting the web host (so they need no authentication settings):
///   --lazada-probe [file]  one real feed request; prints a summary and saves the raw response (default lazada-probe.json)
///   --lazada-import        one import into the MariaDB named by ConnectionStrings:MariaDb
/// Credentials come from environment variables: Lazada__Affiliate__AppKey, __AppSecret, __UserToken (and optional filters).
/// </summary>
public static class LazadaCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddEnvironmentVariables().Build();
        var settings = new LazadaAffiliateOptions();
        config.GetSection("Lazada:Affiliate").Bind(settings);
        if (!settings.IsConfigured)
        {
            Console.Error.WriteLine("Set Lazada__Affiliate__AppKey, Lazada__Affiliate__AppSecret and Lazada__Affiliate__UserToken.");
            return 2;
        }
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var client = new LazadaAffiliateClient(http, Options.Create(settings), TimeProvider.System);
        try
        {
            return args[0] == "--lazada-probe"
                ? await ProbeAsync(client, settings, args.Length > 1 ? args[1] : "lazada-probe.json")
                : await ImportAsync(client, settings, config);
        }
        catch (LazadaApiException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
        catch (HttpRequestException e)
        {
            Console.Error.WriteLine($"Could not reach Lazada: {e.Message}");
            return 1;
        }
    }

    private static async Task<int> ProbeAsync(LazadaAffiliateClient client, LazadaAffiliateOptions settings, string outputPath)
    {
        var category = settings.CategoryIds.Length > 0 ? settings.CategoryIds[0] : (long?)null;
        var response = await client.GetProductFeedAsync(1, 5, category, settings.ProductIds.Length > 0 ? settings.ProductIds.Take(5).ToArray() : null, CancellationToken.None);
        await File.WriteAllTextAsync(outputPath, response.GetRawText());
        Console.WriteLine($"Saved the raw response to {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"Top-level fields: {string.Join(", ", response.ValueKind == System.Text.Json.JsonValueKind.Object ? response.EnumerateObject().Select(p => p.Name) : ["(not an object)"])}");
        var products = LazadaFeedParser.FindProducts(response);
        if (products.Count == 0)
        {
            LazadaFeedParser.ThrowIfError(response);
            Console.WriteLine("The request worked but returned no products.");
            return 0;
        }
        Console.WriteLine($"Products found: {products.Count}");
        var now = TimeProvider.System.GetUtcNow();
        foreach (var item in products)
        {
            var row = LazadaFeedParser.Map(item, new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero), null, out var reason);
            Console.WriteLine(row is null
                ? $"  skipped: {reason}"
                : $"  {row.ExternalProductId}  {row.Price:0.00} {row.Currency}  {(row.InStock ? "in stock" : "out of stock")}  {row.Title}");
        }
        return 0;
    }

    private static async Task<int> ImportAsync(LazadaAffiliateClient client, LazadaAffiliateOptions settings, IConfiguration config)
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
        using var loggers = LoggerFactory.Create(b => b.AddSimpleConsole());
        var connector = new LazadaAffiliateConnector(client, Options.Create(settings), clock, loggers.CreateLogger<LazadaAffiliateConnector>());
        var importer = new FeedImporter(db, new DataMutationLock(), clock, new AlertEvaluator(db, clock));
        var result = await importer.ImportAsync(connector);
        Console.WriteLine($"Imported: {result.ProductsAdded} new products, {result.ObservationsAdded} new observations, {result.ObservationsSkipped} already stored.");
        return 0;
    }
}
