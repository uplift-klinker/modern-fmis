import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "@/testing/render-with-providers";
import { TestingApiServer } from "@/testing/testing-api-server";
import { ModelFactory } from "@/testing/model-factory";
import { ClientDetail } from "@/features/clients/components/client-detail";

describe("ClientDetail", () => {
  it("shows the client name, email, and phone", async () => {
    const client = ModelFactory.createClient({
      name: "Acme Farms",
      email: "ops@acme.example",
      phoneNumber: "555-0100",
    });
    TestingApiServer.setupGetClient(client);

    renderWithProviders(<ClientDetail clientId={client.id} />);

    expect(await screen.findByRole("heading", { name: "Acme Farms" })).toBeInTheDocument();
    expect(screen.getByText(/ops@acme\.example/)).toBeInTheDocument();
    expect(screen.getByText(/555-0100/)).toBeInTheDocument();
  });

  it("shows a not-found state when the client request 404s", async () => {
    const client = ModelFactory.createClient();
    TestingApiServer.setupGetClient(client, { status: 404 });

    renderWithProviders(<ClientDetail clientId={client.id} />);

    expect(await screen.findByText(/client not found/i)).toBeInTheDocument();
  });
});
