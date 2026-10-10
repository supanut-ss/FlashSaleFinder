# Marketplaces

ProDiff stores one price history per marketplace listing. A marketplace can have more than one feed (an official API, files exported from its portal); each feed is a small connector, and everything else is shared: the importer, the scheduler and the status report.

| Marketplace | Feed | State |
|---|---|---|
| Lazada | Affiliate Open API | Connector written; waits for the approved API key, secret and user token. See [operations.md](operations.md#lazada-affiliate-feed). |
| Lazada, Shopee, TikTok | **Files exported from the portal** | Connector written and tested on sample files; waits for a real export to confirm the columns. See below. |
| Shopee | Affiliate Open API | Not written: its reference is behind the affiliate login and access may not be granted to this account. |
| TikTok | Shop / affiliate API | Not written: access, scopes and price fields are unconfirmed. |

The app shows this: `GET /api/status` lists every marketplace with its access level, the Account screen shows "ยังไม่เชื่อมต่อ" (not connected), "นำเข้าจากไฟล์" (imported from files) or "ข้อมูลตัวอย่าง" (sample), and filtering to a marketplace with no feed explains why there are no results instead of saying nothing matched.

## File feeds (Shopee, TikTok, Lazada)

Portals for affiliates and sellers usually let you download a product or offer list. ProDiff reads those files, so the data comes from your own account and no API access or API terms are involved.

1. In the portal, look for a product/offer list you can **download** (Product Offer, Product Feed, Export, Download). Save it as `.csv` or `.json`. Keep each download as a separate file; the file's modified time is the capture time unless the file has its own timestamp column.
2. Preview it without importing anything:
   `./scripts/feed.ps1 -Marketplace Shopee -Action Probe -Path C:\exports\shopee`
   It prints the headers, which column was matched to which field, the first rows as they would be stored, and why any row would be skipped. Headers are matched against known English and Thai names; if one is wrong or missing, name it in an env file, for example `FileFeeds__Shopee__Columns__Price=Sale Price` (fields: `ProductId`, `ShopId`, `Title`, `Price`, `OriginalPrice`, `Category`, `Url`, `ImageUrl`, `AffiliateUrl`, `Stock`, `InStock`, `Currency`, `CapturedAt`).
3. Import once: `./scripts/feed.ps1 -Marketplace Shopee -Action Import -Path C:\exports\shopee -EnvFile <file with ConnectionStrings__MariaDb>`.
4. Or let the API do it on a schedule outside demo mode: set `FileFeeds__Shopee__Path` to a file or a folder (all `.csv`, `.json` and `.jsonl` files in it, oldest first) and `FileFeeds__Shopee__IntervalMinutes` (default 60, minimum 15). Drop each new export into the folder; importing a file again stores nothing new.

What is stored and what is not:
- Required columns: a product id, a title and a price. A shop id column, when present, is combined with the product id (`shopId.productId`) because a Shopee item id alone is not unique.
- Prices are read from forms like `฿1,299.00`. Ranges ("199 - 399"), prices with a decimal comma (`299,5`), zero, more than two decimals and non-THB currencies are skipped and counted rather than guessed.
- Links are kept only if they are HTTPS. An affiliate link comes only from a column that holds one; nothing is built locally.
- A timestamp without an offset is read as Thailand time. A future timestamp is skipped.
- A row's price is the price in the export, not necessarily what a buyer sees (vouchers and personalised discounts are not in a list).
- Excel files (`.xlsx`) are not read yet; save as CSV. If your portal only offers `.xlsx`, say so and support can be added once a real file is available.

Sample files for trying the commands are in [samples](samples).

## How a marketplace plugs in

1. Write a class implementing `IMarketplaceSource` (`Connectors/IMarketplaceConnector.cs`): a unique name, the marketplace, whether it is configured, how often to import, its capability, and `ReadAsync`, which yields `FeedProduct` rows. Keep the response parsing in its own static class so it can be tested with sample payloads (see `LazadaFeedParser`, `FileFeedMapper`).
2. Put its credentials or settings in an options class bound from configuration and return `IsConfigured = false` until they are all present. An unconfigured source is never run and reports `Unavailable`.
3. Register it in `Program.cs` next to Lazada: `AddScoped<IMarketplaceSource>(...)`. `MarketplaceIngestService` then imports it on its own schedule (never faster than every 15 minutes), and a failure in one source does not affect the others. A marketplace may have several sources; the status report shows the best connected one.
4. Rows must be valid for `FeedImporter`: THB, at most two decimals, unique observation ids per product and capture, HTTPS URLs, no future timestamps. Skip and count anything else rather than failing the run.
5. Add tests: request signing against a vector computed independently (for APIs), the parser against variants and error responses, paging against a fake HTTP handler, and an import that is not marked as a sample.
6. Add a probe command so the first real response or file can be inspected before the first import.

Rules that apply to every marketplace:
- Use the marketplace's own tracking or affiliate links only; never build one locally.
- Price history comes from repeated imports. A feed that returns only the current price still produces history, but it starts empty.
- Never present a discount as proven unless the history supports it (the existing evidence rules already enforce this).
- Read the marketplace's terms on storing and displaying prices before turning a connector on in production.

## Shopee API (not written)

What is known comes from third-party sources, not from Shopee, so treat it as unverified:
- Shopee runs an **affiliate Open API** that is separate from the seller Open Platform (the one with `partner_id`, which serves only your own shop and uses a different signature). Third-party SDKs describe it as GraphQL with an app id and a secret, offer data under a query called `productOfferV2`, and a regional host (the Brazilian host is `open-api.affiliate.shopee.com.br`; the Thailand host was not found).
- The signing scheme for the affiliate API was not confirmed. Do not reuse the seller API formula.
- The reference at `affiliate.shopee.co.th/open_api` needs an affiliate login and could not be read here. If your account is not eligible, use a file feed instead.

If you do get access, send screenshots of the reference pages (endpoint, signing, the product-offer query with its arguments and returned fields, paging, rate limits and terms), with the secret and app id covered.

## TikTok API (not written)

Also from third-party sources, unverified:
- TikTok Shop has a developer platform reached through **Partner Center**, with separate regional portals. Credentials do not carry across portals, and an app's scopes decide what data it can read. TikTok has announced Affiliate APIs, but the endpoints, fields and which markets they cover were not confirmed, and it is not known whether product prices are included.
- Approval depends on the account category: a seller building for its own shop may be approved quickly, other categories need business documents.
- If TikTok offers no official price feed for third parties, the right outcome is "not connected" or a file feed from your own account, not a workaround.

## What not to do

- Do not scrape marketplace pages, run a crawler or a browser extension that visits pages for you, or buy scraped data. It breaks the platforms' terms, needs bot-detection workarounds, and cannot give the traceable evidence this app promises.
- Do not mix seller APIs (your own shop's data) with affiliate APIs (market-wide offers); they authorize and sign differently.
- A browser extension is not part of this design. If one is ever added, it must only record the page the user is looking at when they press a button, label the result as user-reported and unverified (which needs a provenance column in the database), and have its own authentication; check the marketplace's terms first.

## Matching products across marketplaces

Each marketplace listing is stored separately. `ComparableVariantKey` exists so that two listings can be declared the same product, but no matching is done yet and nothing is compared across marketplaces. Do that only with explicit, reviewed matches.
