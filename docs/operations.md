# Local operation and production gates

## Demo

Follow the explicit Development command in the README. Data is in memory and disappears when the process stops. The demo session represents one shared local user, not independent registered accounts.

The sample endpoint `POST /api/demo/tick` requires a demo bearer token and `{ "productId": "<id>" }`. It records a locally simulated 20% price decrease and checks target alerts. This is a demonstration action; it does not change any marketplace listing. A repeated tick for the same item requires waiting at least one second.

## MariaDB and Redis

Start Docker Desktop before these commands. Set a local password outside version control:

```powershell
$env:MARIADB_PASSWORD = '<local-password>'
$env:MARIADB_ROOT_PASSWORD = '<local-root-password>'
docker compose up -d
```

The default host ports are 3306 and 6379. If either is already in use, set `MARIADB_PORT` or `REDIS_PORT` before `docker compose up -d` and use the selected port in the matching connection string.

Persistence uses Microting's EF Core 10 provider for MySQL-compatible databases, configured for MariaDB 11.4. Keep the provider and EF Core package versions aligned when upgrading.

The initial MariaDB schema is mounted into the initialization directory and executes only for a fresh database volume. Existing databases require an explicitly reviewed schema upgrade; the application never silently drops or recreates production tables.

Configure the API using environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
Demo__Enabled=false
ConnectionStrings__MariaDb=Server=localhost;Port=3306;Database=flashsale;User=flashsale;Password=<local-password>
ConnectionStrings__Redis=localhost:6379
Authentication__Authority=https://<trusted-oidc-issuer>
Authentication__Audience=<api-audience>
```

The authority must expose valid OpenID Connect discovery metadata and signing keys. The application does not issue production access tokens. Missing production configuration causes startup to fail. Configuring production infrastructure does not enable a live marketplace connector.

## Choosing the database by environment

`scripts/dev.ps1` starts the API in Development demo mode and picks the database from its parameters and the git-ignored `.env` file (see `.env.example`):

```powershell
./scripts/dev.ps1 -Database Memory                       # sample data in memory (default)
./scripts/dev.ps1 -Database MariaDb                      # MariaDB + Redis from docker compose
./scripts/dev.ps1 -Database MariaDb -DatabaseName other  # a different schema on the same server
```

A settings file may instead hold a ready-made `ConnectionStrings__MariaDb=...` line (pass it with `-EnvFile`; a UTF-8 BOM is tolerated). Because demo mode writes synthetic sample rows, the script refuses a database host that is not this machine unless you pass `-AllowRemote`; use that only for a scratch database. Pass `-NoRedis` to keep the cache in memory when you do not want to touch whatever Redis is listening locally.

The script builds the connection strings from `.env` without printing the password, checks that MariaDB is reachable, and stops with a clear message if it is not. The Account screen of the web client shows which database and cache the API is really using (backed by `GET /api/status`), so a wrongly set environment is visible at a glance. The browser tests compare it with `E2E_EXPECT_DB` (`InMemory` by default, `MariaDB` when run against a MariaDB-backed API).

Screenshots of every main screen at desktop, tablet and mobile sizes for both modes are in `docs/screenshots/memory` and `docs/screenshots/mariadb`. Regenerate them against a running API with `node scripts/capture-screens.mjs --base <url> --out <folder>` from `src/FlashSale.Web`. The script signs in to the demo account and simulates a price drop, so point it at a database you do not mind changing (for MariaDB, a scratch schema loaded from `schema.sql`).

## Demo on MariaDB

By default the Development demo keeps its data in memory. To exercise the real database path with sample data, start the Compose services and opt in with `Demo__Database=MariaDb`. Demo mode is still Development-only, and the demo identity and synthetic data are unchanged; only storage moves to MariaDB, so the watchlist, alerts and price history survive restarts.

```powershell
docker compose up -d
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Demo__Enabled = 'true'
$env:Demo__Database = 'MariaDb'
$env:ConnectionStrings__MariaDb = 'Server=localhost;Port=3306;Database=flashsale;User=flashsale;Password=<local-password>'
dotnet run --project src/FlashSale.Api --no-launch-profile --urls http://localhost:5080
```

At startup the API imports the sample feed into the database. The import is idempotent, so restarts do not duplicate listings or observations. The schema must already exist (it is applied by Compose on a fresh volume); the API does not create tables. If the database is unreachable the API stops with a message pointing at `docker compose up -d`. `GET /api/status` reports `databaseProvider: "MariaDB"` when this mode is active. Use the port from `MARIADB_PORT` if you changed it.

The browser tests can target this API instead of their own in-memory one (`$env:E2E_PORT = '5080'`, with `RateLimit__PermitLimit` raised), but they simulate price drops, and on MariaDB those observations persist and lower the sample prices. To return to pristine sample data, run `docker compose down -v` (this deletes the local database volume) and start again.

The MariaDB integration test creates and removes its own rows, so it is safe to re-run against a database that already holds demo data.

## Rate limiting

Requests are limited per client IP, 120 per minute by default. Set `RateLimit__PermitLimit` to change it; the browser tests raise it because they reload the page many times.

## Lazada affiliate feed

The Lazada connector reads the affiliate product feed (`/marketing/product/feed`) through the Open API shown under INTEGRATION > Open API in the Lazada Affiliate portal, and optionally Lazada's own tracking link per product (`/marketing/product/link`). It was written from that portal's reference pages; no public documentation exists for these two endpoints.

What the API gives and does not give:
- It returns the current offer price (`discountPrice`), stock, currency, name, category level 1 (an id) and pictures. There is **no price history, no list price and no product URL**, so ProDiff builds history from repeated imports (hourly by default, never faster than every 15 minutes) and leaves the list price empty.
- Category is stored as "หมวด Lazada <id>" until ids are mapped to names, so these products only appear when the category filter is off.
- Only THB items with a valid price are stored; anything else is skipped and counted in the log.

Setup, once the portal has approved the API request:

1. Copy the key, secret and user token into `.env.lazada` (git-ignored) as `Lazada__Affiliate__AppKey`, `Lazada__Affiliate__AppSecret` and `Lazada__Affiliate__UserToken`; see `.env.example` for the optional filters (category ids, product ids, page size, tracking links).
2. Look at one real response before importing: `./scripts/lazada.ps1 -Action Probe`. It prints the top-level fields and how the first products map, and saves the raw JSON. The signature method, the Thailand gateway (`api.lazada.co.th/rest`) and the success response envelope are implemented from documentation and are only confirmed by a live call; adjust `LazadaFeedParser` if the probe shows a different shape. A call with deliberately fake credentials already reached the gateway and returned an `InvalidAppKey` error, which confirms the host, the path and the error format.
3. Import once: `./scripts/lazada.ps1 -Action Import -EnvFile .env.lazada, <file with ConnectionStrings__MariaDb>`.
4. Run the API with the same settings outside demo mode (which needs the production authentication settings above) and it imports on a schedule (`Lazada__Affiliate__IntervalMinutes`). In demo mode the live connector is never started.

Check the Lazada Open Platform terms before storing and displaying price history, and keep using only the tracking links Lazada returns.

## Schema inspection

```powershell
dotnet run --project src/FlashSale.Api --no-launch-profile -- --write-schema ../../schema-preview.sql
```

This generates SQL without contacting a database. Review the generated script before applying it. Schema generation and query translation tests do not prove MariaDB execution or Redis connectivity.

The MariaDB integration tests are a separate project. Against the Compose database, set its connection string and run them explicitly:

```powershell
$env:MARIADB_TEST_CONNECTION_STRING = 'Server=localhost;Port=3306;Database=flashsale;User=flashsale;Password=<local-password>'
dotnet test tests/FlashSale.Api.MariaDb.Tests/FlashSale.Api.MariaDb.Tests.csproj --configuration Release
```

## Verification boundaries

- `/health` is process liveness, not database or cache readiness.
- Redis registration prepares the cache abstraction; the catalog currently queries persistence directly.
- The app does not calculate a multi-factor deal score: that remains unavailable until data includes the corresponding seller, product rating, popularity and urgency signals. `realDiscountPercent` is a price-to-30-day-average comparison only.
- Full Text Search, query benchmarking, cache retention and provider-specific retry/circuit breakers await the live ingestion phase.
- Deploy behind HTTPS and configure a trusted issuer. Do not expose demo mode or local infrastructure ports to the public internet.
- Hosted CI has three jobs (API tests, web unit and browser tests, MariaDB integration tests against a service container). Their hosted runs have not been observed yet; check the first run after merging.
