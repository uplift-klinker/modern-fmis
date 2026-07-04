import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/testing/render-with-providers";
import { TestingApiServer } from "@/testing/testing-api-server";
import { ModelFactory } from "@/testing/model-factory";
import { ClientList } from "@/features/clients/components/client-list";

describe("ClientList", () => {
  it("shows each client name with contact sub-text", async () => {
    TestingApiServer.setupGetClientList({
      items: [
        ModelFactory.createClient({
          name: "Acme Farms",
          email: "ops@acme.example",
          phoneNumber: "555-0100",
        }),
      ],
      totalCount: 1,
    });

    renderWithProviders(<ClientList onSelectClient={() => {}} />);

    expect(await screen.findByText("Acme Farms")).toBeInTheDocument();
    expect(screen.getByText(/ops@acme\.example/)).toBeInTheDocument();
  });

  it("shows a loading indicator while fetching", () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(1), { delayMs: 50 });

    renderWithProviders(<ClientList onSelectClient={() => {}} />);

    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("shows an error state when the request fails", async () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(0), { status: 500 });

    renderWithProviders(<ClientList onSelectClient={() => {}} />);

    expect(await screen.findByText(/couldn't load clients/i)).toBeInTheDocument();
  });

  it("calls onSelectClient with the client id when a client is clicked", async () => {
    const client = ModelFactory.createClient({ name: "Acme Farms" });
    TestingApiServer.setupGetClientList({ items: [client], totalCount: 1 });
    const onSelectClient = vi.fn();

    renderWithProviders(<ClientList onSelectClient={onSelectClient} />);
    await userEvent.click(await screen.findByText("Acme Farms"));

    expect(onSelectClient).toHaveBeenCalledWith(client.id);
  });
});
