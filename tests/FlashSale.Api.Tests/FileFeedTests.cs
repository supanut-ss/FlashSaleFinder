using System.Text;
using FlashSale.Api.Connectors;
using FlashSale.Api.Connectors.Files;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using FlashSale.Api.Watching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlashSale.Api.Tests;

public sealed class FileFeedTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("prodiff-feed-").FullName;
    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string Write(string name, string content, DateTime? modifiedUtc = null)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(path, modifiedUtc ?? DateTime.UtcNow.AddHours(-1));
        return path;
    }

    private FileFeedSource Source(string path, Marketplace marketplace = Marketplace.Shopee, Action<FileFeedOptions>? change = null)
    {
        var options = new FileFeedOptions { Path = path };
        change?.Invoke(options);
        return new FileFeedSource(marketplace, options, TimeProvider.System, NullLogger<FileFeedSource>.Instance);
    }

    private static async Task<List<FeedProduct>> ReadAll(IMarketplaceConnector connector)
    {
        var rows = new List<FeedProduct>();
        await foreach (var row in connector.ReadAsync()) rows.Add(row);
        return rows;
    }

    private const string Csv = "Item ID,Shop ID,Product Name,Price,Original Price,Category,Stock\n" +
                               "111,9,Wireless earbuds,\"฿1,299.00\",\"1,999\",Electronics,5\n" +
                               "222,9,\"Mug, \"\"big\"\"\",199,,Home,0\n";

    [Fact]
    public void CsvHandlesQuotedCommasQuotesNewlinesAndABom()
    {
        var table = TableReader.ReadCsv("﻿a,b\n\"x, y\",\"he said \"\"hi\"\"\"\n\"line1\nline2\",z\n");
        Assert.Equal(["a", "b"], table.Headers);
        Assert.Equal("x, y", table.Rows[0]["a"]);
        Assert.Equal("he said \"hi\"", table.Rows[0]["b"]);
        Assert.Equal("line1\nline2", table.Rows[1]["a"]);
    }

    [Theory]
    [InlineData("a;b\n1;2\n", 1)]
    [InlineData("a\tb\n1\t2\n3\t4\n", 2)]
    [InlineData("a,b\r\n1,2\r\n", 1)]
    public void CsvDelimiterAndLineEndingsAreDetected(string text, int rows)
    {
        var table = TableReader.ReadCsv(text);
        Assert.Equal(["a", "b"], table.Headers);
        Assert.Equal(rows, table.Rows.Count);
        Assert.Equal("1", table.Rows[0]["a"]);
    }

    [Theory]
    [InlineData("""[{"title":"x","price":1}]""")]
    [InlineData("""{"data":{"items":[{"title":"x","price":1}]}}""")]
    [InlineData("{\"title\":\"x\",\"price\":1}\n{\"title\":\"y\",\"price\":2}")]
    public void JsonArraysWrappedArraysAndJsonLinesAreRead(string text)
    {
        var table = TableReader.ReadJson(text);
        Assert.NotEmpty(table.Rows);
        Assert.Equal("x", table.Rows[0]["title"]);
        Assert.Contains("price", table.Headers);
    }

    [Theory]
    [InlineData("Item ID", "Product Name", "Price")]
    [InlineData("itemid", "ITEM_NAME", "sale-price")]
    [InlineData("รหัสสินค้า", "ชื่อสินค้า", "ราคา")]
    public void ColumnsAreMatchedByEnglishAndThaiNames(string id, string title, string price)
    {
        var map = FileFeedMapper.Detect([id, title, price, "other"], new Dictionary<string, string>());
        Assert.Equal(id, map.Get("ProductId"));
        Assert.Equal(title, map.Get("Title"));
        Assert.Equal(price, map.Get("Price"));
    }

    [Fact]
    public void ExplicitColumnNamesOverrideDetection()
    {
        var map = FileFeedMapper.Detect(["A", "B", "C", "Price"], new Dictionary<string, string> { ["ProductId"] = "A", ["Title"] = "B", ["Price"] = "C" });
        Assert.Equal("C", map.Get("Price"));
    }

    [Fact]
    public void MissingRequiredColumnsExplainWhatToSet()
    {
        var error = Assert.Throws<FileFeedException>(() => FileFeedMapper.Detect(["foo", "bar"], new Dictionary<string, string>()));
        Assert.Contains("ProductId", error.Message);
        Assert.Contains("Headers found: foo, bar", error.Message);
        Assert.Contains("Columns__ProductId", error.Message);
        Assert.Throws<FileFeedException>(() => FileFeedMapper.Detect(["a"], new Dictionary<string, string> { ["Title"] = "nope" }));
    }

    [Theory]
    [InlineData("฿1,299.00", 1299.00)]
    [InlineData("199", 199)]
    [InlineData("1 299,5", 0)]
    [InlineData("บาท 45.50", 45.50)]
    public void PricesAreParsedFromCommonFormats(string text, double expected)
    {
        var ok = FileFeedMapper.TryPrice(text, out var price, out _);
        if (expected == 0) Assert.False(ok); else Assert.Equal((decimal)expected, price);
    }

    [Theory]
    [InlineData("199 - 399", "price is a range")]
    [InlineData("abc", "no usable price")]
    [InlineData("0", "no usable price")]
    [InlineData("10.123", "price has more than two decimals")]
    [InlineData("", "no price")]
    public void UnusablePricesAreRefusedWithAReason(string text, string expected)
    {
        Assert.False(FileFeedMapper.TryPrice(text, out _, out var reason));
        Assert.Equal(expected, reason);
    }

    [Fact]
    public async Task MapsRowsFromAnExportedFile()
    {
        var rows = await ReadAll(Source(Write("shopee.csv", Csv)));

        Assert.Equal(2, rows.Count);
        var earbuds = rows.Single(r => r.ExternalProductId == "9.111");
        Assert.Equal(1299.00m, earbuds.Price);
        Assert.Equal(1999m, earbuds.OriginalPrice);
        Assert.Equal("Electronics", earbuds.Category);
        Assert.True(earbuds.InStock);
        Assert.StartsWith("file-shopee-9.111-", earbuds.SourceObservationId);
        var mug = rows.Single(r => r.ExternalProductId == "9.222");
        Assert.Equal("Mug, \"big\"", mug.Title);
        Assert.False(mug.InStock);
        Assert.Null(mug.OriginalPrice);
    }

    [Fact]
    public async Task OnlyHttpsLinksAreKeptAndAffiliateLinksComeFromTheirOwnColumn()
    {
        var path = Write("links.csv", "id,name,price,url,image,affiliate link\n1,A,10,http://insecure/x,https://img/x.jpg,https://s.shopee.co.th/abc\n");
        var row = Assert.Single(await ReadAll(Source(path)));
        Assert.Null(row.ProductUrl);
        Assert.Equal("https://img/x.jpg", row.ImageUrl);
        Assert.Equal("https://s.shopee.co.th/abc", row.AffiliateUrl);
    }

    [Fact]
    public async Task CaptureTimeIsTheFileTimeUnlessTheFileCarriesItsOwn()
    {
        var modified = new DateTime(2026, 10, 9, 3, 4, 5, DateTimeKind.Utc);
        var byFile = Assert.Single(await ReadAll(Source(Write("a.csv", "id,name,price\n1,A,10\n", modified))));
        Assert.Equal(new DateTimeOffset(modified, TimeSpan.Zero), byFile.CapturedAt);
        Assert.Equal("file-shopee-1-20261009030405", byFile.SourceObservationId);

        // A bare timestamp is Thailand local time; one with an offset is taken as written.
        var rows = await ReadAll(Source(Write("b.csv", "id,name,price,date\n1,A,10,2026-10-08 14:30:00\n2,B,20,2026-10-08T10:00:00Z\n")));
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 7, 30, 0, TimeSpan.Zero), rows.Single(r => r.ExternalProductId == "1").CapturedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero), rows.Single(r => r.ExternalProductId == "2").CapturedAt);
    }

    [Fact]
    public async Task RowsTheAppMustNotStoreAreSkipped()
    {
        var text = "id,name,price,currency,date\n" +
                   "1,Good,10,THB,\n" +
                   "2,Dollars,10,USD,\n" +
                   "3,Range,\"10 - 20\",THB,\n" +
                   "4,Future,10,THB,2999-01-01\n" +
                   "5,Dup,10,THB,\n5,Dup again,11,THB,\n" +
                   ",NoId,10,THB,\n";
        var rows = await ReadAll(Source(Write("mixed.csv", text)));
        Assert.Equal(["1", "5"], rows.Select(r => r.ExternalProductId).Order());
        Assert.Equal(10m, rows.Single(r => r.ExternalProductId == "5").Price);
    }

    [Fact]
    public async Task AFolderIsReadOldestFirstAndKeepsEachFilesOwnTime()
    {
        Write("new.csv", "id,name,price\n1,A,80\n", new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc));
        Write("old.csv", "id,name,price\n1,A,100\n", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Write("notes.txt", "ignored");
        var rows = await ReadAll(Source(folder));
        Assert.Equal([100m, 80m], rows.Select(r => r.Price));
        Assert.Equal(2, rows.Select(r => r.SourceObservationId).Distinct().Count());
    }

    [Fact]
    public async Task ImportingTheSameFileTwiceStoresNothingNew()
    {
        var source = Source(Write("shopee.csv", Csv));
        var clock = TimeProvider.System;
        await using var db = new FlashSaleDbContext(new DbContextOptionsBuilder<FlashSaleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var importer = new FeedImporter(db, new DataMutationLock(), clock, new AlertEvaluator(db, clock));

        var first = await importer.ImportAsync(source);
        var second = await importer.ImportAsync(source);

        Assert.Equal((2, 2), (first.ProductsAdded, first.ObservationsAdded));
        Assert.Equal((0, 0), (second.ProductsAdded, second.ObservationsAdded));
        var product = await db.Products.FirstAsync();
        Assert.Equal(Marketplace.Shopee, product.Marketplace);
        Assert.False(product.IsSample);
    }

    [Fact]
    public void CapabilityReflectsWhetherTheFilesExist()
    {
        var missing = Source(Path.Combine(folder, "nope.csv")).Capability;
        Assert.Equal(ConnectorAccess.Unavailable, missing.Access);
        Assert.Contains("not found", missing.Status);
        Assert.Equal(ConnectorAccess.AuthorizedFeed, Source(Write("x.csv", Csv)).Capability.Access);
        Assert.Equal(ConnectorAccess.Unavailable, Source("").Capability.Access);
        Assert.False(Source("").IsConfigured);
    }

    [Fact]
    public async Task PreviewShowsMatchedColumnsAndTheFirstRows()
    {
        var preview = await Source(Write("shopee.csv", Csv)).PreviewAsync(Path.Combine(folder, "shopee.csv"), 5);
        Assert.Equal(2, preview.RowCount);
        Assert.Equal("Product Name", preview.Columns.Get("Title"));
        Assert.Contains(preview.Lines, l => l.Contains("9.111") && l.Contains("1299.00 THB"));
    }

    [Fact]
    public async Task FilesOverTheSizeLimitAreRefused()
    {
        var path = Write("big.csv", Csv);
        await Assert.ThrowsAsync<FileFeedException>(() => ReadAll(Source(path, change: o => o.MaxFileBytes = 10)));
    }

    [Fact]
    public async Task TheColumnOverridesFromConfigurationAreUsed()
    {
        var path = Write("odd.csv", "X,Y,Z\n7,Thing,55\n");
        var source = Source(path, change: o => { o.Columns["ProductId"] = "X"; o.Columns["Title"] = "Y"; o.Columns["Price"] = "Z"; });
        Assert.Equal(55m, Assert.Single(await ReadAll(source)).Price);
    }
}
