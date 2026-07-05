import { describe, it, expect } from "vitest";
import { EmailSchema } from "@/shared/schemas/email-schema";

describe("EmailSchema", () => {
  it("accepts a valid email", () => {
    expect(EmailSchema.safeParse("ops@acme.example").success).toBe(true);
  });

  it("rejects a malformed email", () => {
    expect(EmailSchema.safeParse("a").success).toBe(false);
  });

  it("can be made optional with nullable", () => {
    expect(EmailSchema.nullable().safeParse(null).success).toBe(true);
  });
});
