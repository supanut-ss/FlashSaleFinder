# ProDiff (FlashSaleFinder)

A sample-first price research app. The original roadmap is reviewed in [docs/review.md](docs/review.md); phase ownership and gates are tracked in [docs/implementation-plan.md](docs/implementation-plan.md).

The Development web app supports search, price evidence, watchlists, in-app target alerts and a clearly labelled local price-drop simulation. A multi-factor Deal Score remains unavailable because the sample source lacks the seller, rating, popularity and urgency evidence needed to support it.

## Development

Requires the .NET 10 SDK. Demo mode is explicitly enabled and supported only in Development. Demo records are synthetic; restarting the in-memory app resets its data.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Demo__Enabled = 'true'
dotnet run --project src/FlashSale.Api --no-launch-profile --urls http://localhost:5080
```

The web client is a React app (Vite, plain JavaScript) in `src/FlashSale.Web`. Its build output goes to `src/FlashSale.Api/wwwroot/` (git-ignored), so build it before running the API:

```powershell
cd src/FlashSale.Web
npm install
npm run build   # production bundle served by the API at http://localhost:5080
npm run dev     # optional: hot-reload at http://localhost:5173, proxies /api to localhost:5080
```

Web tests live in the same folder. `npm test` runs unit tests; `npm run test:e2e` builds the bundle, starts the demo API on port 5081 and drives it in an installed Chrome on desktop, tablet and mobile sizes (set `PW_CHANNEL=msedge` to use Edge):

```powershell
cd src/FlashSale.Web
npm test
npm run test:e2e
```

Choose where demo data lives with one command: `./scripts/dev.ps1 -Database Memory` (default) or `./scripts/dev.ps1 -Database MariaDb` (needs `docker compose up -d` and a `.env` copied from `.env.example`). See [docs/operations.md](docs/operations.md#choosing-the-database-by-environment); screenshots of both modes are in [docs/screenshots](docs/screenshots).

```powershell
dotnet build FlashSaleFinder.sln
dotnet test FlashSaleFinder.sln
```

If using the repository's RTK convention, prefix commands with `rtk proxy`.

## Boundaries

- No authorized marketplace feed or API credentials were supplied. Sample data does not represent current marketplace prices, actual discounts, availability or affiliate attribution.
- Live Lazada integration is the next external integration gate. Shopee and TikTok remain future phases pending their own authorized contracts.
- Production uses MariaDB, Redis and a configured trusted JWT issuer. Secrets belong in environment variables or a secret store; do not commit them.
- Local demo identity and data are for development only. Do not expose demo mode publicly.
- Production deployment, push/email providers and affiliate account registration are outside the sample implementation.

## Architecture

The API is a modular monolith. A product identifies one marketplace listing variant; observations retain decimal THB prices and UTC timestamps. Connector capabilities allow feed-only providers without requiring unsupported remote search. The web client shares the API origin.

See [docs/ui-design.md](docs/ui-design.md) for the interface direction and [docs/implementation-plan.md](docs/implementation-plan.md) for verification evidence and outstanding gates.
