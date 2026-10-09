# Flash Hunt web design

## Direction
Build a Thai shopping research tool, with search and inspectable price evidence as the primary experience. Serve a small HTML/CSS/JavaScript client from the API so the sample requires one process and no additional frontend toolchain.

The UI/UX skill search returned an ecommerce palette and retail typography, but a marketing showcase with repeated calls to action is inappropriate for a catalog. Use the catalog itself as the opening view.

## Tokens
- Background: `#F5F8FA`; surface: `#FFFFFF`; ink: `#16324A`.
- Primary: `#075985`; savings: `#166534`; attention: `#9A3412`.
- Typography: Tahoma and system sans-serif for dependable Thai rendering without external font requests.
- Layout: left-aligned search, compact filter row, responsive product results and a spacious product detail dialog.
- Price and history carry emphasis. Avoid decorative metrics, invented sales claims and automatic motion.

## Interaction gates
- Permanent sample-data notice; demo history and alerts explicitly identified.
- Keyboard-accessible filters and dialog with visible focus; controls at least 44px tall.
- Loading, empty and error states; failed requests retain recovery actions.
- Price-history chart includes a text equivalent and clear observation dates.
- Affiliate links only when supplied and validated; never invent tracking links.
- Verify narrow/mobile and desktop layouts and no horizontal overflow.

## Implemented page

`src/FlashSale.Api/wwwroot/` contains the same-origin catalog and product dialog. It uses native SVG for the chart, an accessible table as its text equivalent, and DOM text nodes for provider strings. The browser stores the short-lived demo token in session storage. The demo account is shared on that browser, and it is explicitly marked as sample data.

Playwright smoke verification covered desktop and mobile viewports, catalog/detail loading, demo login, watchlist target creation, and a simulated alert. Full browser flows must still be repeated after any API contract or visual redesign.
