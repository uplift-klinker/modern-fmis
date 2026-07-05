import { describe, it, expect } from "vitest";
import { PhoneNumberSchema } from "@/shared/schemas/phone-number-schema";

describe("PhoneNumberSchema", () => {
  it("accepts a phone number with a country code", () => {
    expect(PhoneNumberSchema.safeParse("+1 (555) 555-0100").success).toBe(true);
  });

  it("rejects a phone number without a country code", () => {
    expect(PhoneNumberSchema.safeParse("(555) 555-0100").success).toBe(false);
  });

  it("rejects a phone number that is too short", () => {
    expect(PhoneNumberSchema.safeParse("+1 555").success).toBe(false);
  });

  it("can be made optional with nullable", () => {
    expect(PhoneNumberSchema.nullable().safeParse(null).success).toBe(true);
  });
});
