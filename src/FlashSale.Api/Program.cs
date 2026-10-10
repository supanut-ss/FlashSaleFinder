using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FlashSale.Api.Catalog;
using FlashSale.Api.Connectors;
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
builder.Services.AddScoped<LazadaAffiliateConnector>();
var lazadaLive = !demo && (builder.Configuration.GetSection("Lazada:Affiliate").Get<LazadaAffiliateOptions>()?.IsConfigured ?? false);
if (lazadaLive) builder.Services.AddHostedService<LazadaIngestService>();
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
app.MapGet("/api/status", (RuntimeMode mode) => Results.Ok(new
{
    mode.IsDemo, mode.DatabaseProvider, mode.CacheProvider,
    DataLabel = mode.IsDemo ? "Sample data only; not live marketplace prices."
        : lazadaLive ? "Lazada affiliate feed; price history builds from repeated imports. Other marketplaces are not connected."
        : "No live marketplace connector configured.",
    Marketplaces = MarketplaceCapabilities.Unavailable.Select(x =>
        x.Marketplace != FlashSale.Api.Domain.Marketplace.Lazada ? x
        : demo ? new ConnectorCapability(x.Marketplace, ConnectorAccess.SampleFeed, true, "Synthetic local fixture; no marketplace connection.")
        : lazadaLive ? new ConnectorCapability(x.Marketplace, ConnectorAccess.OfficialApi, true, "Lazada Affiliate Open API product feed.")
        : x)
}));
app.MapProductEndpoints();
app.MapInsightEndpoints();
app.MapWatchEndpoints();
app.MapIdentityEndpoints(demo);
if (demo) app.MapDemoEndpoints();
app.Run();

public partial class Program;
