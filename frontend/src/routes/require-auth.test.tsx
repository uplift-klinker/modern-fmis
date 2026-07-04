import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router-dom";
import { renderWithProviders } from "@/testing/render-with-providers";
import { RequireAuth } from "@/routes/require-auth";

function Protected() {
  return (
    <Routes>
      <Route element={<RequireAuth />}>
        <Route path="/clients" element={<div>secret clients</div>} />
      </Route>
      <Route path="/unauthorized" element={<div>unauthorized page</div>} />
    </Routes>
  );
}

describe("RequireAuth", () => {
  it("renders protected content when authenticated", () => {
    renderWithProviders(<Protected />, { route: "/clients", auth: { isAuthenticated: true } });

    expect(screen.getByText("secret clients")).toBeInTheDocument();
  });

  it("triggers login with the returnTo path when unauthenticated", () => {
    const login = vi.fn();

    renderWithProviders(<Protected />, {
      route: "/clients",
      auth: { isAuthenticated: false, login },
    });

    expect(login).toHaveBeenCalledWith("/clients");
    expect(screen.queryByText("secret clients")).not.toBeInTheDocument();
  });

  it("redirects to /unauthorized on auth error", () => {
    renderWithProviders(<Protected />, {
      route: "/clients",
      auth: { isAuthenticated: false, hasError: true },
    });

    expect(screen.getByText("unauthorized page")).toBeInTheDocument();
  });
});
