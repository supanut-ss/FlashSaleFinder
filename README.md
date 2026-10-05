# Flash Hunt / FlashSaleFinder

A sample-first price research app. The original roadmap is reviewed in [docs/review.md](docs/review.md); phase ownership and gates are tracked in [docs/implementation-plan.md](docs/implementation-plan.md).

## Development

Requires the .NET 10 SDK. Demo mode is explicitly enabled and supported only in Development. Demo records are synthetic; restarting the in-memory app resets its data.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Demo__Enabled = 'true'
dotnet run --project src/FlashSale.Api --no-launch-profile --urls http://localhost:5080
```

```powershell
dotnet build FlashSaleFinder.sln
dotnet test FlashSaleFinder.sln
```

If using the repository's RTK convention, prefix commands with `rtk proxy`.

## Boundaries

- No authorized marketplace feed or API credentials were supplied. Sample data does not represent current marketplace prices, actual discounts, availability or affiliate attribution.
- Live Lazada integration is the next external integration gate. Shopee and TikTok remain future phases pending their own authorized contracts.
- Production uses PostgreSQL, Redis and a configured trusted JWT issuer. Secrets belong in environment variables or a secret store; do not commit them.
- Local demo identity and data are for development only. Do not expose demo mode publicly.
- Production deployment, push/email providers and affiliate account registration are outside the sample implementation.

## Architecture

The API is a modular monolith. A product identifies one marketplace listing variant; observations retain decimal THB prices and UTC timestamps. Connector capabilities allow feed-only providers without requiring unsupported remote search. The web client shares the API origin.

See [docs/ui-design.md](docs/ui-design.md) for the interface direction and [docs/implementation-plan.md](docs/implementation-plan.md) for verification evidence and outstanding gates.
