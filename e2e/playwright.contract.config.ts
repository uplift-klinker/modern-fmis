import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./contract",
  globalSetup: "./contract/global-setup.ts",
  globalTeardown: "./contract/global-teardown.ts",
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [["github"], ["list"]] : [["list"]],
  use: {
    baseURL: "http://localhost:8080",
  },
  timeout: 30_000,
  projects: [{ name: "contract" }],
});
