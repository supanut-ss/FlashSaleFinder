import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The production build is served by FlashSale.Api from its wwwroot (same origin).
// In dev, API calls are proxied to the locally running API.
const apiTarget = process.env.API_URL ?? "http://localhost:5080";

export default defineConfig({
  plugins: [react()],
  build: { outDir: "../FlashSale.Api/wwwroot", emptyOutDir: true },
  server: { port: 5173, proxy: { "/api": apiTarget, "/health": apiTarget } },
  test: { include: ["src/**/*.test.js"] },
});
