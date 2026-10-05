# Implementation plan

The supplied project plan is product input, not operational authorization to register accounts or publish services. The repository initially contained only a README.

## Decisions
- Use .NET 10 modular monolith, PostgreSQL persistence, Redis cache and a web MVP.
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
- Docker CLI exists but Docker engine was unavailable at initial inspection. PostgreSQL/Redis runtime checks must be reported separately from in-memory tests.
- Production price freshness, rate limits, cache retention and attribution depend on the granted marketplace contract.

## Verification log
- Initial repository clean; baseline commit `48f2662`.
- Phase 0: scaffold build passed with zero warnings/errors; committed as `a2c96c9`.
- Phase 1: leader independently ran `dotnet test FlashSaleFinder.sln --no-restore`: 15 passed, zero failed. Includes forged-token rejection, production demo rejection, latest-price search, validation, and PostgreSQL SQL translation/schema generation. PostgreSQL/Redis runtime remains unverified because Docker engine is unavailable.
- Phase 1 deviation: search uses provider-compatible case-insensitive substring matching. PostgreSQL Full Text Search and actual query performance remain later production work. Redis is configured through the distributed-cache abstraction; query caching is not yet enabled.
- Official references: [ASP.NET Core JWT validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html).
