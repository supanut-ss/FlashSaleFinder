# ProDiff web design

## Direction
ProDiff is a Thai price-evidence tool: it answers "is this sale real?" by setting the current price against the recorded history. The client is a React (Vite, JavaScript) app in `src/FlashSale.Web`; its production bundle is served from the API's `wwwroot`, so runtime still needs one process. Fonts (IBM Plex Sans Thai, IBM Plex Mono) are self-hosted through `@fontsource`, so there are no external font requests.

The opening view is the product's own evidence: a featured product with its list price struck through, the current price highlighted, and the 30-day price line drawn once on load. The catalog follows as a ledger (one row per product with a current/average/list price bar and a text verdict), not a grid of cards.

## Tokens
- Paper `#F6F7F5`, surface `#FFFFFF`, ink `#12161C`, secondary ink `#4D5764`, rule `#D9DCE0`. Dark theme is designed alongside (`#0F1216` / `#171B21` / `#ECEFF3`).
- Tag yellow `#FFE14D` marks the real price and the current-price points on bars and charts; it is used for nothing else.
- Below average `#0B6B41`, above average `#B0301A` (each paired with an arrow icon and text), interactive blue `#1F4FD8`.
- Typography: IBM Plex Sans Thai for all text; IBM Plex Mono with tabular figures for prices.
- Every text/background pair is checked against WCAG AA (4.5:1, 3:1 for borders and focus rings) in both themes.

## Responsive behaviour
| Width | Shell | Notes |
|---|---|---|
| 1024px and up | Top navigation, hero with search and featured comparison, inline filters, centered dialogs | Ledger rows on one line |
| 640 to 1023px (tablet) | App layout: sticky app bar and search, bottom tab bar, filters in a sheet, detail as a bottom sheet | Featured comparison is two columns; rows use two lines |
| below 640px (mobile) | Same app layout | Single column rows; search submits from the keyboard's search key |

Screens are hash routes (`#/`, `#/watch`, `#/alerts`, `#/account`) so each is deep-linkable. The product detail sheet owns a history entry, so the back gesture closes it. The tab bar and sheets respect device safe areas.

## Interaction gates
- Permanent sample-data notice (banner on wide screens, always-visible tag in the app bar on compact screens).
- Keyboard-accessible navigation, filters, dialogs and chart (arrow keys move the readout); visible focus; pointer targets at least 44px.
- Loading skeletons, empty and error states; failed requests keep a retry action.
- Price-history chart has an accessible summary, a live readout and a table equivalent.
- Motion is limited to the one line-draw and to responses to user actions; `prefers-reduced-motion` disables all of it.
- Affiliate links only when supplied and validated; never invent tracking links.

## Implemented page
Verified with the demo API in a browser pane: build, no console errors, no horizontal overflow and no text under 12px at 1280, 768 and 375px, no interactive control under 44px, and the flows listed below on a 375px viewport: open a product sheet, close it with back and with the button, sign in to the demo, watch with a target, simulate a price drop and see the alert, navigate the tabs, apply a filter in the sheet. Desktop navigation, deep links and the centered dialog were also checked.

Not yet verified: a full keyboard-only pass, landscape orientation, the reduced-motion setting, session expiry (HTTP 401) handling, pagination across more than one page, and a real-device safe-area check.
