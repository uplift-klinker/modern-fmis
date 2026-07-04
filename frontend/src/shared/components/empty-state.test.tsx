import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/testing/render-with-providers";
import { EmptyState } from "@/shared/components/empty-state";

describe("EmptyState", () => {
  it("shows the message and notifies when the refresh button is clicked", async () => {
    const onRefresh = vi.fn();

    renderWithProviders(<EmptyState message="No clients yet." onRefresh={onRefresh} />);

    expect(screen.getByText("No clients yet.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /refresh/i }));

    expect(onRefresh).toHaveBeenCalledTimes(1);
  });
});
