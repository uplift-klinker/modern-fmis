import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router-dom";
import { renderWithProviders } from "@/testing/render-with-providers";
import { AppLayout } from "@/app/app-layout";

function renderLayout(auth: { userEmail?: string; logout?: () => void } = {}) {
  renderWithProviders(
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<div>routed content</div>} />
      </Route>
    </Routes>,
    {
      route: "/",
      auth: {
        isAuthenticated: true,
        userEmail: auth.userEmail ?? "ops@acme.example",
        logout: auth.logout,
      },
    },
  );
}

describe("AppLayout", () => {
  it("shows a user icon menu button rather than the email text", () => {
    renderLayout();

    expect(screen.getByText("routed content")).toBeInTheDocument();
    expect(screen.queryByText("ops@acme.example")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /account menu/i })).toBeInTheDocument();
  });

  it("logs out from the user menu", async () => {
    const logout = vi.fn();
    renderLayout({ logout });

    await userEvent.click(screen.getByRole("button", { name: /account menu/i }));
    await userEvent.click(await screen.findByRole("menuitem", { name: /log out/i }));

    expect(logout).toHaveBeenCalled();
  });
});
