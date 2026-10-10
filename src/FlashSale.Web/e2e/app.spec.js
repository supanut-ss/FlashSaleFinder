import { expect, test } from "@playwright/test";

const isCompact = page => page.locator(".tabbar").count().then(n => n > 0);

test.beforeEach(async ({ page }) => {
  page.on("pageerror", error => { throw error; });
  await page.goto("/");
  await expect(page.locator(".row-item").first()).toBeVisible();
});

/** Opens the demo session through whichever sign-in entry point the current layout has. */
async function signIn(page) {
  if (await isCompact(page)) {
    await page.locator(".tabbar a", { hasText: "บัญชี" }).click();
    await page.getByRole("button", { name: "เข้าโหมดเดโม" }).first().click();
  } else {
    await page.locator(".topnav").getByRole("button", { name: "เข้าโหมดเดโม" }).click();
  }
  await page.getByRole("button", { name: "เข้าใช้บัญชีเดโม" }).click();
  await expect(page.locator(".modal-narrow")).toBeHidden();
}

async function goTo(page, destination) {
  const nav = (await isCompact(page)) ? page.locator(".tabbar") : page.locator(".topnav");
  await nav.getByRole("link", { name: destination }).click();
}

test.describe("shell", () => {
  test("uses the layout that fits the screen", async ({ page }, info) => {
    const compact = info.project.name !== "desktop";
    await expect(page.locator(".tabbar")).toHaveCount(compact ? 1 : 0);
    await expect(page.locator(".topnav")).toHaveCount(compact ? 0 : 1);
    await expect(page.locator(".row-item")).toHaveCount(6);
    await expect(page.getByRole("link", { name: "ProDiff หน้าแรก" })).toBeVisible();
    await expect(page.locator(".brand img").first()).toHaveJSProperty("complete", true);
  });

  test("keeps the sample-data notice in view", async ({ page }) => {
    await expect(page.getByText("ข้อมูลตัวอย่าง").first()).toBeVisible();
  });

  test("has no horizontal overflow, text under 12px or targets under 44px", async ({ page }) => {
    const report = await page.evaluate(() => {
      const small = [];
      document.querySelectorAll("body *").forEach(el => {
        const cs = getComputedStyle(el);
        const hasText = [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim());
        if (cs.display !== "none" && hasText && parseFloat(cs.fontSize) < 12 && !el.closest("svg")) small.push(el.className || el.tagName);
      });
      const tiny = [];
      document.querySelectorAll("a[href],button,select,summary,[tabindex='0']").forEach(el => {
        const r = el.getBoundingClientRect();
        if (!r.width || el.classList.contains("skip-link") || el.classList.contains("row-hit") || el.closest("dialog:not([open])")) return;
        if (r.height < 44 || r.width < 44) tiny.push(`${el.tagName}.${el.className} ${Math.round(r.width)}x${Math.round(r.height)}`);
      });
      return { overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth, small, tiny };
    });
    expect(report).toEqual({ overflow: 0, small: [], tiny: [] });
  });
});

test.describe("catalog", () => {
  test("searches by name and clears back to all products", async ({ page }) => {
    await page.getByRole("searchbox", { name: "ค้นหาสินค้า" }).fill("หม้อทอด");
    await page.getByRole("searchbox", { name: "ค้นหาสินค้า" }).press("Enter");
    await expect(page.locator(".row-item")).toHaveCount(1);
    await expect(page.locator(".row-item")).toContainText("หม้อทอด");

    await page.getByRole("searchbox", { name: "ค้นหาสินค้า" }).fill("ไม่มีสินค้านี้แน่นอน");
    await page.getByRole("searchbox", { name: "ค้นหาสินค้า" }).press("Enter");
    await expect(page.getByText("ยังไม่พบสินค้าที่ตรงกัน")).toBeVisible();
    await page.getByRole("button", { name: "ล้างการค้นหาและตัวกรอง" }).click();
    await expect(page.locator(".row-item")).toHaveCount(6);
  });

  test("filters by category", async ({ page }) => {
    if (await isCompact(page)) {
      await page.getByRole("button", { name: /ตัวกรอง/ }).click();
      await page.locator(".sheet[open] select").nth(1).selectOption("Home");
      await page.getByRole("button", { name: "แสดงผลลัพธ์" }).click();
    } else {
      await page.locator(".toolbar select").nth(1).selectOption("Home");
    }
    await expect(page.locator(".row-item")).toHaveCount(2);
    for (const row of await page.locator(".row-sub").allTextContents()) expect(row).toContain("บ้านและครัว");
  });

  test("pages through results", async ({ page }) => {
    // Seed data has one page; report a larger catalog so the pager has something to do.
    await page.route("**/api/products?*", async route => {
      try {
        const response = await route.fetch();
        const body = await response.json();
        await route.fulfill({ status: response.status(), headers: response.headers(), json: { ...body, total: 13 } });
      } catch {
        // The reload below can cancel a request that is still in flight; that request no longer matters.
      }
    });
    const requested = [];
    page.on("request", request => { if (request.url().includes("/api/products?")) requested.push(new URL(request.url()).searchParams.get("page")); });
    await page.reload();
    await expect(page.getByText("หน้า 1 จาก 3").first()).toBeVisible();
    await expect(page.getByRole("button", { name: "หน้าก่อน" })).toBeDisabled();
    await page.getByRole("button", { name: "หน้าถัดไป" }).click();
    await expect(page.getByText("หน้า 2 จาก 3").first()).toBeVisible();
    expect(requested).toContain("2");
  });

  test("shows a retry when the catalog fails to load", async ({ page }) => {
    await page.route("**/api/products?*", route => route.fulfill({ status: 500, json: { title: "boom" } }));
    await page.reload();
    await expect(page.getByRole("alert")).toContainText("โหลดสินค้าไม่สำเร็จ");
    await page.unroute("**/api/products?*");
    await page.getByRole("button", { name: "ลองอีกครั้ง" }).click();
    await expect(page.locator(".row-item")).toHaveCount(6);
  });
});

test.describe("price evidence", () => {
  test("opens the detail, reads the chart with arrow keys, and closes with Escape", async ({ page }) => {
    await page.locator(".row-hit").first().click();
    const dialog = page.locator("dialog[open]");
    await expect(dialog.locator(".detail-title")).toBeVisible();
    const chart = dialog.locator(".chart svg");
    await expect(chart).toBeVisible();
    await chart.focus();
    await page.keyboard.press("ArrowLeft");
    await expect(dialog.locator(".chart-tip")).toBeVisible();
    await expect(dialog.locator(".chart .sr-only")).toContainText("฿");
    await dialog.locator("summary").click();
    await expect(dialog.locator(".history-table tbody tr").first()).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeHidden();
  });

  test("back gesture closes the detail sheet instead of leaving the app", async ({ page }, info) => {
    test.skip(info.project.name === "desktop", "History-owned sheets are an app-layout behaviour");
    await page.locator(".row-hit").first().click();
    await expect(page.locator("dialog[open]")).toBeVisible();
    await page.goBack();
    await expect(page.locator("dialog[open]")).toBeHidden();
    await expect(page.locator(".row-item").first()).toBeVisible();
  });
});

test.describe("demo account", () => {
  test("watches a product, simulates a drop, shows the alert and unwatches", async ({ page }) => {
    await page.getByRole("searchbox", { name: "ค้นหาสินค้า" }).fill("เซรั่ม");
    await page.getByRole("searchbox", { name: "ค้นหาสินค้า" }).press("Enter");
    await expect(page.locator(".row-item")).toHaveCount(1);
    await page.locator(".row-hit").click();
    const dialog = page.locator("dialog[open]");
    await expect(dialog.locator(".detail-title")).toBeVisible();

    await dialog.getByPlaceholder("เช่น 900").fill("9999");
    await dialog.getByRole("button", { name: "ติดตามสินค้า" }).click();
    // Not signed in yet: the notice asks first, then the action continues.
    await page.getByRole("button", { name: "เข้าใช้บัญชีเดโม" }).click();
    await expect(page.locator(".toast")).toContainText("ตั้งแจ้งเตือน");

    await dialog.getByRole("button", { name: "จำลองราคาลด 20%" }).click();
    await expect(page.locator(".toast")).toContainText("จำลองราคาลด 20%");
    await dialog.getByRole("button", { name: "ปิดรายละเอียด" }).click();

    await goTo(page, "แจ้งเตือน");
    await expect(page.locator(".list-alert").first()).toContainText("ถึงเป้าแล้ว");
    await goTo(page, "ติดตาม");
    await expect(page.locator(".list-item").first()).toContainText("เซรั่ม");
    // Other tests may share the demo user, so only this product has to disappear; retry the click if it is lost.
    const remove = page.getByRole("button", { name: /^เลิกติดตาม เซรั่ม/ });
    await expect(async () => {
      if (await remove.count()) await remove.first().click();
      await expect(remove).toHaveCount(0, { timeout: 3000 });
    }).toPass({ timeout: 15_000 });
    await expect(page.locator(".list-item", { hasText: "เซรั่ม" })).toHaveCount(0);
  });

  test("drops an expired session quietly on load", async ({ page }) => {
    await page.addInitScript(() => sessionStorage.setItem("prodiff-demo-token", "expired.token.value"));
    await page.goto("/#/watch");
    await expect(page.getByText("เข้าโหมดเดโมเพื่อใช้งานส่วนนี้")).toBeVisible();
    await expect.poll(() => page.evaluate(() => sessionStorage.getItem("prodiff-demo-token"))).toBeNull();
  });

  test("tells the user when the session expires mid-action", async ({ page }) => {
    await signIn(page);
    await page.route("**/api/watchlist", route => route.request().method() === "POST"
      ? route.fulfill({ status: 401, json: { title: "Unauthorized" } }) : route.continue());
    await page.goto("/");
    await page.locator(".row-hit").first().click();
    await page.locator("dialog[open]").getByRole("button", { name: "ติดตามสินค้า" }).click();
    await expect(page.locator(".toast-error")).toContainText("เซสชันเดโมหมดอายุ");
  });
});

test.describe("keyboard and motion", () => {
  test("opens a product using only the keyboard and returns focus on close", async ({ page }, info) => {
    test.skip(info.project.name !== "desktop", "Keyboard path is covered once, on desktop");
    let reached = false;
    for (let i = 0; i < 80 && !reached; i++) {
      await page.keyboard.press("Tab");
      reached = await page.evaluate(() => document.activeElement?.classList.contains("row-hit"));
    }
    expect(reached).toBe(true);
    const title = await page.evaluate(() => document.activeElement.textContent);
    await page.keyboard.press("Enter");
    await expect(page.locator("dialog[open] .detail-title")).toHaveText(title);
    await page.keyboard.press("Escape");
    await expect(page.locator("dialog[open]")).toBeHidden();
    expect(await page.evaluate(() => document.activeElement?.classList.contains("row-hit"))).toBe(true);
  });

  test("skip link moves focus to the main content", async ({ page }, info) => {
    test.skip(info.project.name !== "desktop", "Keyboard path is covered once, on desktop");
    await page.keyboard.press("Tab");
    await expect(page.getByRole("link", { name: "ข้ามไปเนื้อหาหลัก" })).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(page.locator("#main")).toBeFocused();
  });

  test("reduced motion removes the chart draw animation", async ({ page }) => {
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.reload();
    const line = page.locator(".compare .chart-line");
    await expect(line).toBeVisible();
    expect(await line.evaluate(el => getComputedStyle(el).animationName)).toBe("none");
  });

  test("keeps the app layout usable in landscape", async ({ page }, info) => {
    test.skip(info.project.name !== "mobile", "Landscape phones only");
    await page.setViewportSize({ width: 844, height: 390 });
    await page.reload();
    await expect(page.locator(".tabbar")).toBeInViewport();
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBe(0);
    await page.locator(".row-hit").first().click();
    const sheet = page.locator("dialog[open]");
    await expect(sheet.locator(".detail-title")).toBeVisible();
    const box = await sheet.boundingBox();
    expect(box.height).toBeLessThanOrEqual(390);
  });

  test("renders in dark mode with a readable background", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "dark" });
    const background = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    const [r, g, b] = background.match(/\d+/g).map(Number);
    expect((r + g + b) / 3).toBeLessThan(60);
    await expect(page.locator(".brand img").first()).toBeVisible();
  });
});
