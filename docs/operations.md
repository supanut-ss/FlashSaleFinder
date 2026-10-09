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
- Hosted CI is configured but only local checks have been observed in this session.
