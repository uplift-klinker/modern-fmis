import { describe, it, expect } from "vitest";
import {
  ClientResponseSchema,
  CreateClientRequestSchema,
  ClientListSchema,
} from "@/features/clients/schemas/client-schemas";

describe("client schemas", () => {
  it("parses a client response", () => {
    const parsed = ClientResponseSchema.parse({
      id: "11111111-1111-1111-1111-111111111111",
      name: "Acme Farms",
      email: "ops@acme.example",
      phoneNumber: null,
    });

    expect(parsed.name).toBe("Acme Farms");
  });

  it("accepts a create request with only a phone number", () => {
    const result = CreateClientRequestSchema.safeParse({
      name: "Acme",
      email: null,
      phoneNumber: "555-0100",
    });

    expect(result.success).toBe(true);
  });

  it("rejects a create request with neither email nor phone", () => {
    const result = CreateClientRequestSchema.safeParse({
      name: "Acme",
      email: null,
      phoneNumber: null,
    });

    expect(result.success).toBe(false);
  });

  it("rejects a blank name", () => {
    const result = CreateClientRequestSchema.safeParse({
      name: "",
      email: "ops@acme.example",
      phoneNumber: null,
    });

    expect(result.success).toBe(false);
  });

  it("parses a list result", () => {
    const result = ClientListSchema.safeParse({ items: [], totalCount: 0 });

    expect(result.success).toBe(true);
  });
});
