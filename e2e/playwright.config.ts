import { defineConfig, devices } from "@playwright/test";
import { requireEnv } from "./support/config";

export default defineConfig({
  testDir: "./system",
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [["github"], ["list"]] : [["list"]],
  use: {
    baseURL: requireEnv("E2E_FRONTEND_URL"),
    trace: "on-first-retry",
  },
  projects: [{ name: "system", use: { ...devices["Desktop Chrome"] } }],
});
