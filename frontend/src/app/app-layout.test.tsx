import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router-dom";
import { renderWithProviders } from "@/testing/render-with-providers";
import { AppLayout } from "@/app/app-layout";

describe("AppLayout", () => {
  it("renders the routed content and logs out the signed-in user", async () => {
    const logout = vi.fn();

    renderWithProviders(
      <Routes>
        <Route element={<AppLayout />}>
          <Route path="/" element={<div>routed content</div>} />
        </Route>
      </Routes>,
      { route: "/", auth: { isAuthenticated: true, userEmail: "ops@acme.example", logout } },
    );

    expect(screen.getByText("routed content")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /ops@acme\.example/i }));

    expect(logout).toHaveBeenCalled();
  });
});
