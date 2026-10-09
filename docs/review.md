# Plan review

## Findings addressed in the implementation scope

1. **Listing identity:** marketplace IDs alone are insufficient. Preserve variant identity and currency on observations; never compare different capacities or currencies as the same product.
2. **History confidence:** a single observation cannot establish a 30-day discount. Return an insufficient-history state and explain the calculation window. Sampling frequency must not weight frequently refreshed days more heavily.
3. **Feed capabilities:** feed-only accounts cannot satisfy remote product search or on-demand pricing. Search the local catalog and expose connector capabilities rather than inventing API access.
4. **Personal data:** watchlists and alerts must obtain ownership from authenticated identity. Caller-supplied user IDs are not authorization.
5. **Import semantics:** duplicate observations must be idempotent, old observations must not replace the latest price, and timestamps must be UTC.
6. **Alert semantics:** record in-app events with deduplication; live push/email delivery is a separate integration requiring credentials and delivery verification.

## External dependencies

Marketplace account registration, API/feed eligibility, approved payloads, caching/display permissions, attribution and production notification providers remain external dependencies. The user confirmed no current access and selected sample data first.

## Production gates

- Execute schema and integration tests against MariaDB and Redis.
- Configure a trusted JWT issuer/audience and validate real signed tokens.
- Validate authorized provider payloads, quotas, retention and affiliate URLs.
- Verify persistence, backup, deployment configuration and notification delivery.
- Recalibrate the provisional deal formula with actual data; scores are not a guarantee of value.

No live marketplace, deployment or notification delivery is claimed by the demo.
