// Captures the main screens at desktop, tablet and mobile sizes against a running API.
//   node scripts/capture-screens.mjs --base http://localhost:5080 --out ../../docs/screenshots/mariadb
// Uses an installed Chrome (PW_CHANNEL=msedge for Edge). It signs in to the demo account, watches a product
// and simulates a price drop so the watchlist and alerts screens have content. Run it against a database you
// do not mind changing (the simulated drop is stored when the API uses MariaDB).
import { chromium } from "@playwright/test";
import { mkdirSync } from "node:fs";
import { join } from "node:path";

const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, item, i, all) => (item.startsWith("--") ? [...pairs, [item.slice(2), all[i + 1]]] : pairs), []));
const base = args.base ?? "http://localhost:5080";
const out = args.out ?? "screenshots";
mkdirSync(out, { recursive: true });

const DEVICES = {
  desktop: { viewport: { width: 1280, height: 800 } },
  tablet: { viewport: { width: 820, height: 1180 }, hasTouch: true },
  mobile: { viewport: { width: 375, height: 812 }, hasTouch: true, isMobile: true },
};

const browser = await chromium.launch({ channel: process.env.PW_CHANNEL ?? "chrome" });
const shot = (page, device, name, fullPage = false) =>
  page.screenshot({ path: join(out, `${device}-${name}.jpg`), type: "jpeg", quality: 82, fullPage });

// A fixed tab bar would float mid-image in a full-page shot, so grow the viewport to the page height instead.
const shotTall = async (page, device, name) => {
  const size = page.viewportSize();
  const height = await page.evaluate(() => document.documentElement.scrollHeight);
  await page.setViewportSize({ width: size.width, height: Math.max(size.height, height) });
  await page.waitForTimeout(300);
  await shot(page, device, name);
  await page.setViewportSize(size);
};

const settle = async page => {
  await page.locator(".row-item").first().waitFor();
  await page.locator(".compare .chart svg").first().waitFor({ state: "visible" }).catch(() => {});
  await page.waitForTimeout(1600); // let the one-time line-draw finish
};

async function openDetail(page) {
  await page.locator(".row-hit").first().click();
  const dialog = page.locator("dialog[open]");
  await dialog.locator(".chart svg").waitFor();
  await dialog.locator(".chart svg").focus();
  await page.keyboard.press("ArrowLeft");
  await page.keyboard.press("ArrowLeft");
  await page.waitForTimeout(500);
  return dialog;
}

for (const [device, options] of Object.entries(DEVICES)) {
  const compact = device !== "desktop";
  const context = await browser.newContext({ ...options, colorScheme: "light", locale: "th-TH" });
  const page = await context.newPage();
  await page.goto(base);
  await settle(page);
  await shot(page, device, "1-explore", device === "desktop");

  const dialog = await openDetail(page);
  await shot(page, device, "2-detail");

  // Sign in from inside the product (first run also creates a watch + a price drop so later screens have data).
  await dialog.getByPlaceholder("เช่น 900").fill("9999");
  await dialog.getByRole("button", { name: "ติดตามสินค้า" }).click();
  await page.getByRole("button", { name: "เข้าใช้บัญชีเดโม" }).click();
  await page.locator(".toast").first().waitFor();
  await dialog.getByRole("button", { name: "ปิดรายละเอียด" }).click();
  await dialog.waitFor({ state: "hidden" });

  if (compact) {
    await page.locator(".tabbar a", { hasText: "ติดตาม" }).click();
  } else {
    await page.locator(".topnav").getByRole("link", { name: /รายการติดตาม/ }).click();
  }
  await page.locator(".list-item").first().waitFor();
  await shot(page, device, "3-watchlist");

  if (compact) await page.locator(".tabbar a", { hasText: "แจ้งเตือน" }).click();
  else await page.locator(".topnav").getByRole("link", { name: /แจ้งเตือน/ }).click();
  await page.waitForTimeout(500);
  await shot(page, device, "4-alerts");

  await page.goto(`${base}/#/account`);
  await page.getByRole("region", { name: "สถานะระบบ" }).locator("dd").first().waitFor();
  // Dismiss any toast so it does not cover the panel, and capture the whole page: the status panel sits below the fold.
  await page.locator(".toast button").click().catch(() => {});
  await page.waitForTimeout(300);
  if (compact) await shotTall(page, device, "5-account");
  else await shot(page, device, "5-account", true);

  if (device === "mobile") {
    await page.goto(`${base}/#/`);
    await settle(page);
    await page.getByRole("button", { name: /ตัวกรอง/ }).click();
    await page.locator(".sheet[open]").waitFor();
    await page.waitForTimeout(500);
    await shot(page, device, "6-filters");
    await page.getByRole("button", { name: "ปิดตัวกรอง" }).click();

    await page.emulateMedia({ colorScheme: "dark" });
    await page.waitForTimeout(500);
    await shot(page, device, "7-explore-dark");
  }
  await context.close();
}

await browser.close();
console.log(`Saved screenshots to ${out}`);
