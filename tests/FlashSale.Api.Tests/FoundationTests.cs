using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FlashSale.Api.Catalog;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FlashSale.Api.Tests;

public sealed class FoundationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(1.234)]
    [InlineData(10000000000)]
    public void MoneyRejectsInvalidDecimalAmounts(decimal amount) => Assert.Throws<ArgumentException>(() => DomainRules.Money(amount, "THB"));

    [Fact]
    public void MoneyPreservesSatangAndRejectsOtherCurrencies()
    {
        DomainRules.Money(19.99m, "THB");
        Assert.Throws<ArgumentException>(() => DomainRules.Money(10, "USD"));
        Assert.Throws<ArgumentException>(() => DomainRules.Utc(DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(7))));
    }

    [Fact]
    public async Task PersistenceRejectsInvalidObservationBeforeSaving()
    {
        using var db = new FlashSaleDbContext(new DbContextOptionsBuilder<FlashSaleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.PriceObservations.Add(new PriceObservation { Price = -1, SourceObservationId = "bad", CapturedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<ArgumentException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DemoSessionProtectsPersonalEndpointsAndRejectsForgedSignature()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        var response = await client.PostAsync("/api/demo/session", null);
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<DemoSession>())!;
        Assert.True(session.IsDemo);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
        var forged = new JwtSecurityToken(IdentityConfiguration.DemoIssuer, IdentityConfiguration.DemoAudience,
            [new Claim("sub", "victim")], expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(new byte[64]), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(forged));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task SearchFiltersLatestObservedPriceAndPagesVariants()
    {
        await using var factory = new ApiFactory();
        var first = Product("one", "small");
        first.Observations.Add(Observation(999, "old", -1));
        first.Observations.Add(Observation(19.99m, "latest", 0));
        var second = Product("one", "large");
        second.Observations.Add(Observation(35.50m, "latest", 0));
        var third = Product("two", "small");
        third.Marketplace = Marketplace.Shopee;
        third.Observations.Add(Observation(1, "latest", 0));
        await factory.SeedAsync(db => db.Products.AddRange(first, second, third));
        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?q=TEST&marketplace=lazada&minPrice=10&maxPrice=40&sort=priceAsc&page=2&pageSize=1");
        Assert.Equal(2, response!.Total);
        Assert.Equal(second.Id, Assert.Single(response.Items).Id);
        var detail = await client.GetFromJsonAsync<ProductDto>($"/api/products/{first.Id}");
        Assert.Equal(19.99m, detail!.CurrentPrice);
        Assert.Equal("small", detail.ExternalVariantId);
        var prices = await client.GetFromJsonAsync<PagedResult<PriceDto>>($"/api/products/{first.Id}/prices");
        Assert.Equal(new[] { 19.99m, 999m }, prices!.Items.Select(x => x.Price));
    }

    [Theory]
    [InlineData("/api/products?page=0")]
    [InlineData("/api/products?pageSize=101")]
    [InlineData("/api/products?sort=unsafe")]
    [InlineData("/api/products?marketplace=unknown")]
    [InlineData("/api/products?minPrice=20&maxPrice=10")]
    public async Task InvalidSearchIsBadRequest(string url)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public void DemoCannotStartInProduction()
    {
        using var factory = new ApiFactory(true, "Production");
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Development", exception.Message);
    }

    [Fact]
    public void ProductionWithoutExternalAuthenticationFailsClosed()
    {
        using var factory = new ApiFactory(false, "Production");
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Authentication:Authority", exception.Message);
    }

    [Fact]
    public void PostgreSqlModelHasRequiredConstraintsAndTranslatesCatalogQuery()
    {
        using var db = new FlashSaleDbContext(new DbContextOptionsBuilder<FlashSaleDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test").Options);
        var schema = db.Database.GenerateCreateScript();
        Assert.Contains("numeric(12,2)", schema);
        Assert.Contains("CK_Observations_Currency", schema);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Products_Marketplace_ExternalProductId_ExternalVariantId\"", schema);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_WatchlistEntries_UserId_ProductId\"", schema);
        var query = ProductEndpoints.Project(db.Products).Where(x => x.CurrentPrice >= 10).OrderBy(x => x.CurrentPrice).Take(20).ToQueryString();
        Assert.Contains("SELECT", query);
        Assert.Contains("LIMIT", query);
    }

    private static Product Product(string external, string variant) => new()
    {
        Title = "Test product", Category = "test", VariantLabel = variant,
        ExternalProductId = external, ExternalVariantId = variant, Marketplace = Marketplace.Lazada, IsSample = true
    };

    private static PriceObservation Observation(decimal price, string source, int days) => new()
    {
        Price = price, SourceObservationId = source, CapturedAt = DateTimeOffset.UtcNow.AddDays(days)
    };
}
