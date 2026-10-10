using System.Net;
using System.Text;
using System.Text.Json;
using FlashSale.Api.Connectors;
using FlashSale.Api.Connectors.Lazada;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlashSale.Api.Tests;

public sealed class LazadaTests
{
    private static string Products(params string[] items) => """{"data":{"products":[""" + string.Join(",", items) + """]}}""";

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static string Item(long id, string price = "\"199.50\"", string currency = "THB", string extra = "") =>
        $$"""{"productId":{{id}},"productName":"หูฟังไร้สาย {{id}}","categoryL1":10,"pictures":["https://img.lazcdn.com/a.jpg"],"outOfStock":false,"stock":5,"currency":"{{currency}}","discountPrice":{{price}}{{extra}}}""";

    [Fact]
    public void SignerMatchesAnIndependentlyComputedVector()
    {
        var parameters = new Dictionary<string, string>
        {
            ["userToken"] = "tok-abc", ["page"] = "1", ["limit"] = "20", ["offerType"] = "1",
            ["app_key"] = "123456", ["timestamp"] = "1517886554000", ["sign_method"] = "sha256",
        };
        // Computed with Node's crypto over "/marketing/product/feed" + sorted name+value pairs.
        Assert.Equal("D9F82282AC45D096DE0626EFA1844B6AF759FD4BCC16B2B64C7C0B85783CF749",
            LazadaSigner.Sign("/marketing/product/feed", parameters, "helloworld"));
    }

    [Fact]
    public void SignerIgnoresEmptyValuesAndAnExistingSignature()
    {
        var plain = new Dictionary<string, string> { ["a"] = "1" };
        var noisy = new Dictionary<string, string> { ["a"] = "1", ["empty"] = "", ["sign"] = "OLD" };
        Assert.Equal(LazadaSigner.Sign("/x", plain, "s"), LazadaSigner.Sign("/x", noisy, "s"));
    }

    [Theory]
    [InlineData("""{"data":{"products":[{"productId":1}]}}""")]
    [InlineData("""{"result":{"data":[{"productId":1}]},"code":"0"}""")]
    [InlineData("""{"result":"{\"products\":[{\"productId\":1}]}"}""")]
    [InlineData("""[{"productId":1}]""")]
    public void ProductsAreFoundWhereverTheEnvelopePutsThem(string response) =>
        Assert.Single(LazadaFeedParser.FindProducts(Json(response)));

    [Fact]
    public void ApiErrorsAreReportedAndSuccessCodesAreNot()
    {
        var error = Assert.Throws<LazadaApiException>(() => LazadaFeedParser.ThrowIfError(Json("""{"code":"IllegalAccessToken","message":"bad token"}""")));
        Assert.Contains("IllegalAccessToken", error.Message);
        LazadaFeedParser.ThrowIfError(Json("""{"code":"0","data":{"products":[]}}"""));
        LazadaFeedParser.ThrowIfError(Json("""{"data":{"products":[]}}"""));
    }

    [Fact]
    public void MapsAFeedItemToAnObservation()
    {
        var captured = new DateTimeOffset(2026, 10, 10, 8, 0, 0, TimeSpan.Zero);
        var row = LazadaFeedParser.Map(Json(Item(123456)), captured, "https://c.lazada.co.th/t/c.abc", out var reason);
        Assert.Null(reason);
        Assert.NotNull(row);
        Assert.Equal("123456", row.ExternalProductId);
        Assert.Equal(199.50m, row.Price);
        Assert.Equal("THB", row.Currency);
        Assert.True(row.InStock);
        Assert.Equal("หมวด Lazada 10", row.Category);
        Assert.Equal("https://img.lazcdn.com/a.jpg", row.ImageUrl);
        Assert.Equal("https://c.lazada.co.th/t/c.abc", row.AffiliateUrl);
        Assert.Null(row.OriginalPrice);
        Assert.Equal("lazada-aff-123456-20261010080000", row.SourceObservationId);
    }

    [Fact]
    public void OutOfStockAndPlainHttpLinksAreHandled()
    {
        var row = LazadaFeedParser.Map(Json(Item(1, extra: ",\"outOfStock\":true")), DateTimeOffset.UtcNow, "http://insecure.example/x", out _);
        Assert.NotNull(row);
        Assert.False(row.InStock);
        Assert.Null(row.AffiliateUrl);
    }

    [Theory]
    [InlineData("\"199.5\"", "USD", "currency is not THB")]
    [InlineData("0", "THB", "no usable discountPrice")]
    [InlineData("\"abc\"", "THB", "no usable discountPrice")]
    [InlineData("\"10.123\"", "THB", "price has more than two decimals")]
    public void ItemsTheAppMustNotStoreAreSkippedWithAReason(string price, string currency, string expected)
    {
        Assert.Null(LazadaFeedParser.Map(Json(Item(1, price, currency)), DateTimeOffset.UtcNow, null, out var reason));
        Assert.Equal(expected, reason);
    }

    private sealed class FakeLazada(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(request), Encoding.UTF8, "application/json") });
        }
    }

    private static LazadaAffiliateOptions Settings(Action<LazadaAffiliateOptions>? change = null)
    {
        var settings = new LazadaAffiliateOptions { AppKey = "key", AppSecret = "secret", UserToken = "token", PageSize = 2, MaxPages = 3 };
        change?.Invoke(settings);
        return settings;
    }

    private static LazadaAffiliateConnector Connector(FakeLazada handler, LazadaAffiliateOptions settings) =>
        new(new LazadaAffiliateClient(new HttpClient(handler), Options.Create(settings), TimeProvider.System),
            Options.Create(settings), TimeProvider.System, NullLogger<LazadaAffiliateConnector>.Instance);

    [Fact]
    public async Task ReadsPagesUntilShortPageAndSignsEveryRequest()
    {
        var handler = new FakeLazada(request =>
        {
            var page = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"];
            return page == "1" ? Products(Item(1), Item(2)) : Products(Item(2), Item(3));
        });
        var settings = Settings(s => s.PageSize = 2);
        var rows = await ReadAll(Connector(handler, settings));

        // Page 2 repeats product 2; it must be stored once. Page 2 is full, so page 3 is requested too (empty here).
        Assert.Equal(["1", "2", "3"], rows.Select(r => r.ExternalProductId).Order());
        var first = System.Web.HttpUtility.ParseQueryString(handler.Requests[0].Query);
        Assert.Equal("1", first["offerType"]);
        Assert.Equal("token", first["userToken"]);
        Assert.Equal("key", first["app_key"]);
        Assert.Equal("2", first["limit"]);
        var expected = LazadaSigner.Sign("/marketing/product/feed",
            first.AllKeys.Where(k => k != "sign").ToDictionary(k => k!, k => first[k]!), "secret");
        Assert.Equal(expected, first["sign"]);
        Assert.StartsWith("https://api.lazada.co.th/rest/marketing/product/feed?", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task FetchesTrackingLinksOnlyUpToTheConfiguredLimit()
    {
        var handler = new FakeLazada(request => request.RequestUri!.AbsolutePath.EndsWith("/marketing/product/link")
            ? """{"data":{"trackingLink":"https://c.lazada.co.th/t/c.xyz"}}"""
            : Products(Item(1), Item(2)));
        var rows = await ReadAll(Connector(handler, Settings(s => { s.MaxTrackingLinks = 1; s.MaxPages = 1; })));

        Assert.Equal("https://c.lazada.co.th/t/c.xyz", rows.Single(r => r.ExternalProductId == "1").AffiliateUrl);
        Assert.Null(rows.Single(r => r.ExternalProductId == "2").AffiliateUrl);
        Assert.Single(handler.Requests, r => r.AbsolutePath.EndsWith("/link"));
    }

    [Fact]
    public async Task AnErrorResponseWithNoProductsFailsTheRun()
    {
        var handler = new FakeLazada(_ => """{"code":"IllegalAccessToken","message":"bad token"}""");
        await Assert.ThrowsAsync<LazadaApiException>(() => ReadAll(Connector(handler, Settings())));
    }

    [Fact]
    public async Task MissingCredentialsAreRejectedBeforeAnyRequest()
    {
        var handler = new FakeLazada(_ => "{}");
        var connector = Connector(handler, Settings(s => s.AppSecret = ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAll(connector));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ImportsLiveRowsThatAreNotMarkedAsSamples()
    {
        var handler = new FakeLazada(_ => Products(Item(7)));
        var clock = TimeProvider.System;
        await using var db = new FlashSaleDbContext(new DbContextOptionsBuilder<FlashSaleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var importer = new FeedImporter(db, new DataMutationLock(), clock, new AlertEvaluator(db, clock));
        var connector = Connector(handler, Settings(s => s.MaxPages = 1));

        var first = await importer.ImportAsync(connector);
        Assert.Equal(1, first.ProductsAdded);
        Assert.Equal(1, first.ObservationsAdded);

        var product = await db.Products.Include(x => x.Observations).SingleAsync();
        Assert.False(product.IsSample);
        Assert.Equal(Marketplace.Lazada, product.Marketplace);
        Assert.Equal(199.50m, product.Observations.Single().Price);
    }

    private static async Task<List<FeedProduct>> ReadAll(IMarketplaceConnector connector)
    {
        var rows = new List<FeedProduct>();
        await foreach (var row in connector.ReadAsync()) rows.Add(row);
        return rows;
    }
}
