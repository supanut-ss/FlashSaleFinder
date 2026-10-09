# Implementation plan

The supplied project plan is product input, not operational authorization to register accounts or publish services. The repository initially contained only a README.

## Decisions
- Use .NET 10 modular monolith, MariaDB persistence, Redis cache and a web MVP.
- Start with explicitly labelled sample data; no marketplace credentials are available. Never fabricate official API contracts or affiliate tracking links.
- Keep prices in decimal THB and separate marketplace product/variant identities. Compare only explicitly matched variants.
- Price history must contain captured observations; insufficient history must not appear as proven savings.
- Authentication protects personal data; the server derives ownership from authenticated claims.
- No deployment, external notifications or account registration in this implementation.

## Ordered phases and gates
| Phase | Scope | Owner | Gate |
|---|---|---|---|
| 0 | Review, solution, execution plan | Lead + review agent | Clean scaffold build |
| 1 | Domain, database, connector interface, API, authentication and Redis wiring | Backend agent; lead integrates | Build and focused automated tests |
| 2 | Explicit demo feed, idempotent imports, price history and deal metrics | Backend agent; lead integrates | Import/history regression tests |
| 3 | Web search, filters, product detail and watch/alert demo flow | UI agent; lead integrates | Build, API integration checks and UI smoke checks |
| External gate | Official Lazada, Shopee and TikTok integration; production push | Pending account access | Authorized feed/API documentation and credentials |

Phases are committed independently after review. The lead owns integration and verification. Agents must have non-overlapping file ownership and may not create subagents.

## Known constraints
- .NET SDK 10.0.401 is installed.
- Docker CLI exists; its engine was unavailable during the original phases. The database follow-up started Docker Desktop and verified the current MariaDB setup separately from in-memory tests.
- Production price freshness, rate limits, cache retention and attribution depend on the granted marketplace contract.

## Verification log

The phase entries below record the original PostgreSQL implementation before the database-provider follow-up at the end of this log.

- Initial repository clean; baseline commit `48f2662`.
- Phase 0: scaffold build passed with zero warnings/errors; committed as `a2c96c9`.
- Phase 1: leader independently ran `dotnet test FlashSaleFinder.sln --no-restore`: 15 passed, zero failed. Includes forged-token rejection, production demo rejection, latest-price search, validation, and PostgreSQL SQL translation/schema generation. PostgreSQL/Redis runtime remains unverified because Docker engine is unavailable.
- Phase 1 deviation: search uses provider-compatible case-insensitive substring matching. PostgreSQL Full Text Search and actual query performance remain later production work. Redis is configured through the distributed-cache abstraction; query caching is not yet enabled.
- Phase 2: demo feed imports six clearly labelled synthetic listings with 35–90 deterministic daily THB observations. Import validates currency/timestamps/immutable source IDs and is idempotent; daily baselines exclude today and weight each observed UTC date once. Savings require fresh data and at least seven prior observed days. A multi-factor Deal Score remains unavailable until seller/product rating, popularity and urgency inputs exist, so its value stays null.
- Phase 2: owner-scoped watchlists, target alerts and in-app events; retry-safe event IDs use the alert and price observation pair. A Development-only endpoint simulates a 20% price drop. No external notifications or marketplace calls.
- Phase 2 verification: leader ran `dotnet test FlashSaleFinder.sln --configuration Release --no-restore`: 26 passed, 0 failed. `dotnet build FlashSaleFinder.sln --configuration Release --no-restore`: passed with 0 warnings/errors. `git diff --check`: clean. PostgreSQL/Redis runtime not verified; Docker engine is unavailable.
- PostgreSQL SQL schema regenerated for the phase 2 entities. The provided compose init script applies only to a new database volume; an existing installation needs a reviewed migration.
- Phase 2 integration review found a first-login race: parallel watchlist/alert requests could create duplicate demo users with the in-memory provider. `CurrentUser` now serializes user creation through the shared mutation lock. A concurrent 16-request regression test verifies one user row and 200 responses.
- Phase 3: added same-origin responsive Thai catalog, filters, product detail with SVG chart and price table, price evidence/original price, demo login, owned watchlists, in-app alert list and local price-drop simulation. No marketplace link is fabricated, and discount percentage is never called Deal Score.
- Phase 3 UI smoke: Playwright desktop (1280px) and mobile (375px) showed 6 products and up to 60 history observations; searched headphones, opened details, authenticated, set a ฿900 target, simulated ฿990→฿792, and observed the in-app alert. No horizontal overflow or page errors.
- Final integration verification: `dotnet test FlashSaleFinder.sln --configuration Release --no-restore` passed 27/27; Release build passed with 0 warnings/errors; `node --check` and `git diff --check` passed. PostgreSQL/Redis runtime remains unverified because Docker engine is unavailable.
- Official references: [ASP.NET Core JWT validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html).
- Database provider follow-up: switched persistence and schema generation to MariaDB 11.4 with Microting's EF Core 10 provider. The provider is a community-maintained Pomelo fork; see the operations guide for version-pinning details.
- MariaDB verification: Release build passed with 0 warnings/errors; the main suite passed 28/28; the separate MariaDB integration suite passed 1/1 against MariaDB 11.4. Runtime checks covered schema initialization and check constraints, catalog query translation/execution, repeated feed import at `datetime(6)` precision, case-sensitive identity values, and cascade delete. Package vulnerability audit reported no vulnerable API dependencies.
- Compose runs MariaDB and Redis on the default host ports 3306/6379. This workspace already had services on both ports, so its ignored local `.env` selects 3307/6380; the defaults remain unchanged for other environments.
