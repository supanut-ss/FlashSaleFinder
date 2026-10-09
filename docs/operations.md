# Local operation and production gates

## Demo

Follow the explicit Development command in the README. Data is in memory and disappears when the process stops. The demo session represents one shared local user, not independent registered accounts.

The sample endpoint `POST /api/demo/tick` requires a demo bearer token and `{ "productId": "<id>" }`. It records a locally simulated 20% price decrease and checks target alerts. This is a demonstration action; it does not change any marketplace listing. A repeated tick for the same item requires waiting at least one second.

## PostgreSQL and Redis

Start Docker Desktop before these commands. Set a local password outside version control:

```powershell
$env:POSTGRES_PASSWORD = '<local-password>'
docker compose up -d
```

The initial PostgreSQL schema is mounted into the initialization directory and executes only for a fresh database volume. Existing databases require an explicitly reviewed schema upgrade; the application never silently drops or recreates production tables.

Configure the API using environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
Demo__Enabled=false
ConnectionStrings__Postgres=Host=localhost;Database=flashsale;Username=flashsale;Password=<local-password>
ConnectionStrings__Redis=localhost:6379
Authentication__Authority=https://<trusted-oidc-issuer>
Authentication__Audience=<api-audience>
```

The authority must expose valid OpenID Connect discovery metadata and signing keys. The application does not issue production access tokens. Missing production configuration causes startup to fail. Configuring production infrastructure does not enable a live marketplace connector.

## Schema inspection

```powershell
dotnet run --project src/FlashSale.Api --no-launch-profile -- --write-schema schema-preview.sql
```

This generates SQL without contacting a database. Review the generated script before applying it. Schema generation and query translation tests do not prove PostgreSQL execution or Redis connectivity.

## Verification boundaries

- `/health` is process liveness, not database or cache readiness.
- Redis registration prepares the cache abstraction; the catalog currently queries persistence directly.
- The app does not calculate a multi-factor deal score: that remains unavailable until data includes the corresponding seller, product rating, popularity and urgency signals. `realDiscountPercent` is a price-to-30-day-average comparison only.
- Full Text Search, query benchmarking, cache retention and provider-specific retry/circuit breakers await the live ingestion phase.
- Deploy behind HTTPS and configure a trusted issuer. Do not expose demo mode or local infrastructure ports to the public internet.
- Hosted CI is configured but only local checks have been observed in this session.
