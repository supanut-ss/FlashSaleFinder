using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FlashSale.Api.Catalog;
using FlashSale.Api.Connectors;
using FlashSale.Api.Connectors.Files;
using FlashSale.Api.Connectors.Lazada;
using FlashSale.Api.Data;
using FlashSale.Api.Demo;
using FlashSale.Api.Identity;
using FlashSale.Api.Watching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// Generate a MariaDB schema without connecting to a server or starting the web host.
if (args is ["--write-schema", var schemaPath])
{
    using var db = new FlashSaleDbContext(new DbContextOptionsBuilder<FlashSaleDbContext>()
        .UseMySql("Server=localhost;Database=flashsale;User=schema;Password=schema",
            new MariaDbServerVersion(new Version(11, 4, 0))).Options);
    File.WriteAllText(schemaPath, db.Database.GenerateCreateScript());
    return;
}

// One-off Lazada helpers run without the web host, so they need no authentication settings.
if (args is ["--lazada-probe", ..] or ["--lazada-import", ..])
{
    Environment.ExitCode = await LazadaCommands.RunAsync(args);
    return;
}
if (args is ["--feed-probe", ..] or ["--feed-import", ..])
{
    Environment.ExitCode = await FileFeedCommands.RunAsync(args);
    return;
}

var builder = WebApplication.CreateBuilder(args);
var demo = builder.Configuration.GetValue<bool>("Demo:Enabled");
if (demo && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Demo:Enabled is only permitted in Development.");

// Demo data lives in memory unless a Development run opts into the local MariaDB container (Demo:Database=MariaDb).
var useInMemory = demo && !string.Equals(builder.Configuration["Demo:Database"], "MariaDb", StringComparison.OrdinalIgnoreCase);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DataMutationLock>();
builder.Services.AddScoped<FeedImporter>();
builder.Services.AddScoped<AlertEvaluator>();
builder.Services.Configure<LazadaAffiliateOptions>(builder.Configuration.GetSection("Lazada:Affiliate"));
builder.Services.AddHttpClient<LazadaAffiliateClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
// Each live marketplace registers an IMarketplaceSource; the scheduler and /api/status pick them up. Demo never runs live feeds.
builder.Services.AddScoped<LazadaAffiliateConnector>();
builder.Services.AddScoped<IMarketplaceSource>(sp => sp.GetRequiredService<LazadaAffiliateConnector>());
// Optional file feeds exported from a portal, one per marketplace: FileFeeds__Shopee__Path, FileFeeds__TikTok__Path, ...
foreach (var section in builder.Configuration.GetSection("FileFeeds").GetChildren())
{
    if (!Enum.TryParse<FlashSale.Api.Domain.Marketplace>(section.Key, true, out var feedMarketplace)) continue;
    var feedOptions = section.Get<FileFeedOptions>() ?? new FileFeedOptions();
    builder.Services.AddScoped<IMarketplaceSource>(sp => new FileFeedSource(feedMarketplace, feedOptions,
        sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<FileFeedSource>>()));
}
if (!demo) builder.Services.AddHostedService<MarketplaceIngestService>();
if (demo) builder.Services.AddScoped<SampleFeed>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddIdentity(builder.Configuration, demo);
var mariaDb = builder.Configuration.GetConnectionString("MariaDb");
if (!useInMemory && string.IsNullOrWhiteSpace(mariaDb))
    throw new InvalidOperationException(demo
        ? "Demo:Database=MariaDb requires ConnectionStrings:MariaDb."
        : "ConnectionStrings:MariaDb is required outside explicit demo mode.");
builder.Services.AddDbContext<FlashSaleDbContext>(options =>
{
    if (useInMemory) options.UseInMemoryDatabase(builder.Configuration["Demo:DatabaseName"] ?? "flashsale-demo");
    else options.UseMySql(mariaDb, new MariaDbServerVersion(new Version(11, 4, 0)));
});

var redis = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redis))
{
    if (!demo) throw new InvalidOperationException("ConnectionStrings:Redis is required outside explicit demo mode.");
    builder.Services.AddDistributedMemoryCache();
}
else builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redis;
    options.InstanceName = "flashsale:";
});
builder.Services.AddSingleton(new RuntimeMode(demo, useInMemory ? "InMemory" : "MariaDB", string.IsNullOrWhiteSpace(redis) ? "Memory" : "Redis"));
var permitLimit = builder.Configuration.GetValue("RateLimit:PermitLimit", 120);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: 429, title: "Too many requests", detail: "Please retry in one minute.").ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();
if (demo && builder.Configuration.GetValue("Demo:SeedEnabled", true))
{
    using var scope = app.Services.CreateScope();
    try
    {
        // Import is idempotent, so seeding a persistent database on every start is safe.
        await scope.ServiceProvider.GetRequiredService<FeedImporter>().ImportAsync(scope.ServiceProvider.GetRequiredService<SampleFeed>());
    }
    catch (Exception ex) when (!useInMemory)
    {
        throw new InvalidOperationException(
            "Could not seed demo data into MariaDB. Start it with `docker compose up -d` and check ConnectionStrings:MariaDb.", ex);
    }
}
// Generic responses keep database details and production exception messages out of API output.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { Status = "ok" }));
app.MapGet("/api/status", (RuntimeMode mode, IEnumerable<IMarketplaceSource> sources) =>
{
    var live = sources.Where(x => x.IsConfigured).ToList();
    return Results.Ok(new
    {
        mode.IsDemo, mode.DatabaseProvider, mode.CacheProvider,
        DataLabel = mode.IsDemo ? "Sample data only; not live marketplace prices."
            : live.Count > 0 ? $"Live feeds: {string.Join(", ", live.Select(x => x.Marketplace))}. Price history builds from repeated imports; other marketplaces are not connected."
            : "No live marketplace connector configured.",
        Marketplaces = Enum.GetValues<FlashSale.Api.Domain.Marketplace>().Select(m =>
            demo && m == FlashSale.Api.Domain.Marketplace.Lazada
                ? new ConnectorCapability(m, ConnectorAccess.SampleFeed, true, "Synthetic local fixture; no marketplace connection.")
                : !demo && live.Where(x => x.Marketplace == m).OrderByDescending(x => x.Capability.Access).FirstOrDefault() is { } source ? source.Capability
                : MarketplaceCapabilities.Unavailable(m))
    });
});
app.MapProductEndpoints();
app.MapInsightEndpoints();
app.MapWatchEndpoints();
app.MapIdentityEndpoints(demo);
if (demo) app.MapDemoEndpoints();
app.Run();

public partial class Program;
