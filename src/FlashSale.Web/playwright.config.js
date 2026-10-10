import { defineConfig } from "@playwright/test";

// Runs against the real API in demo mode (in-memory data) serving the built bundle from wwwroot.
// Uses an installed Chrome (set PW_CHANNEL=msedge to use Edge) so no browser download is needed.
const port = process.env.E2E_PORT ?? "5081";
const channel = process.env.PW_CHANNEL ?? "chrome";

export default defineConfig({
  testDir: "./e2e",
  timeout: 45_000,
  expect: { timeout: 10_000 },
  // Demo data and the shared demo user are global to the server, so tests run one at a time.
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: { baseURL: `http://localhost:${port}`, channel, trace: "retain-on-failure" },
  projects: [
    { name: "desktop", use: { viewport: { width: 1280, height: 800 } } },
    { name: "tablet", use: { viewport: { width: 820, height: 1180 }, hasTouch: true } },
    { name: "mobile", use: { viewport: { width: 375, height: 812 }, hasTouch: true, isMobile: true } },
  ],
  webServer: {
    command: `dotnet run --project ../FlashSale.Api --no-launch-profile --urls http://localhost:${port}`,
    url: `http://localhost:${port}/health`,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: { ASPNETCORE_ENVIRONMENT: "Development", Demo__Enabled: "true", Demo__DatabaseName: "e2e", RateLimit__PermitLimit: "100000" },
  },
});
