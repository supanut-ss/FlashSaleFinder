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
