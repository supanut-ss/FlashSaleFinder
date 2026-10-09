using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FlashSale.Api.Catalog;
using FlashSale.Api.Connectors;
using FlashSale.Api.Data;
using FlashSale.Api.Demo;
using FlashSale.Api.Identity;
using FlashSale.Api.Watching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// Generate a PostgreSQL schema without connecting to a server or starting the web host.
if (args is ["--write-schema", var schemaPath])
{
    using var db = new FlashSaleDbContext(new DbContextOptionsBuilder<FlashSaleDbContext>()
        .UseNpgsql("Host=localhost;Database=flashsale;Username=schema").Options);
    File.WriteAllText(schemaPath, db.Database.GenerateCreateScript());
    return;
}

var builder = WebApplication.CreateBuilder(args);
var demo = builder.Configuration.GetValue<bool>("Demo:Enabled");
if (demo && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Demo:Enabled is only permitted in Development.");

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DataMutationLock>();
builder.Services.AddScoped<FeedImporter>();
builder.Services.AddScoped<AlertEvaluator>();
if (demo) builder.Services.AddScoped<SampleFeed>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddIdentity(builder.Configuration, demo);
var postgres = builder.Configuration.GetConnectionString("Postgres");
if (!demo && string.IsNullOrWhiteSpace(postgres))
    throw new InvalidOperationException("ConnectionStrings:Postgres is required outside explicit demo mode.");
builder.Services.AddDbContext<FlashSaleDbContext>(options =>
{
    if (demo) options.UseInMemoryDatabase(builder.Configuration["Demo:DatabaseName"] ?? "flashsale-demo");
    else options.UseNpgsql(postgres);
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
builder.Services.AddSingleton(new RuntimeMode(demo, demo ? "InMemory" : "PostgreSQL", string.IsNullOrWhiteSpace(redis) ? "Memory" : "Redis"));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
    await scope.ServiceProvider.GetRequiredService<FeedImporter>().ImportAsync(scope.ServiceProvider.GetRequiredService<SampleFeed>());
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
    DataLabel = mode.IsDemo ? "Sample data only; not live marketplace prices." : "No live marketplace connector configured.",
    Marketplaces = demo
        ? MarketplaceCapabilities.Unavailable.Select(x => x.Marketplace == FlashSale.Api.Domain.Marketplace.Lazada
            ? new ConnectorCapability(x.Marketplace, ConnectorAccess.SampleFeed, true, "Synthetic local fixture; no marketplace connection.") : x)
        : MarketplaceCapabilities.Unavailable
}));
app.MapProductEndpoints();
app.MapInsightEndpoints();
app.MapWatchEndpoints();
app.MapIdentityEndpoints(demo);
if (demo) app.MapDemoEndpoints();
app.Run();

public partial class Program;
