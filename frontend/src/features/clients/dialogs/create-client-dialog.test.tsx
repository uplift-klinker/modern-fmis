import { describe, it, expect, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/testing/render-with-providers";
import { TestingApiServer } from "@/testing/testing-api-server";
import { RequestCapture } from "@/testing/request-capture";
import { ModelFactory } from "@/testing/model-factory";
import { CreateClientDialog } from "@/features/clients/dialogs/create-client-dialog";
import type { CreateClientRequest } from "@/features/clients/schemas/client-schemas";

describe("CreateClientDialog", () => {
  it("posts the entered client and reports the created result", async () => {
    const created = ModelFactory.createClient({ name: "Acme Farms" });
    const capture = new RequestCapture<CreateClientRequest>();
    TestingApiServer.setupCreateClient(created, { capture });
    const onCreated = vi.fn();
    renderWithProviders(<CreateClientDialog open onClose={() => {}} onCreated={onCreated} />);

    await userEvent.type(screen.getByLabelText(/name/i), "Acme Farms");
    await userEvent.type(screen.getByLabelText(/email/i), "ops@acme.example");
    await userEvent.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(created));
    expect(capture.lastRequest?.body.name).toBe("Acme Farms");
  });

  it("surfaces an error when the create request fails", async () => {
    const created = ModelFactory.createClient();
    TestingApiServer.setupCreateClient(created, { status: 400 });
    renderWithProviders(<CreateClientDialog open onClose={() => {}} onCreated={() => {}} />);

    await userEvent.type(screen.getByLabelText(/name/i), "Acme Farms");
    await userEvent.type(screen.getByLabelText(/email/i), "ops@acme.example");
    await userEvent.click(screen.getByRole("button", { name: /save/i }));

    expect(await screen.findByText(/couldn't create the client/i)).toBeInTheDocument();
  });
});
