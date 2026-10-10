using FlashSale.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlashSale.Api.Tests;

public sealed class ApiFactory(bool demo = true, string environment = "Development", bool seed = false, TimeProvider? clock = null, IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        if (clock is not null) builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton(clock)));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>())
        {
            ["Demo:Enabled"] = demo.ToString(), ["Demo:DatabaseName"] = Guid.NewGuid().ToString(),
            ["Demo:SeedEnabled"] = seed.ToString(),
            ["ConnectionStrings:MariaDb"] = "", ["ConnectionStrings:Redis"] = "",
            ["Authentication:Authority"] = "", ["Authentication:Audience"] = ""
        }));
        return base.CreateHost(builder);
    }

    public async Task SeedAsync(Action<FlashSaleDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlashSaleDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }
}
