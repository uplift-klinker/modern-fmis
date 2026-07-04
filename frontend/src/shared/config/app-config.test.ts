import { describe, it, expect } from "vitest";
import { AppConfigSchema } from "@/shared/config/app-config";

describe("AppConfigSchema", () => {
  it("parses a valid config", () => {
    const result = AppConfigSchema.safeParse({
      apiBaseUrl: "https://api.example.com",
      auth: {
        domain: "tenant.auth0.com",
        clientId: "abc",
        audience: "https://api",
      },
    });

    expect(result.success).toBe(true);
  });

  it("fails when a required field is missing", () => {
    const result = AppConfigSchema.safeParse({
      apiBaseUrl: "https://api.example.com",
    });

    expect(result.success).toBe(false);
  });
});
