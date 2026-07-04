import { useEffect } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { CircularProgress } from "@mui/material";
import { useAuth } from "@/shared/auth/auth-context";

export function RequireAuth() {
  const auth = useAuth();
  const location = useLocation();
  const returnTo = `${location.pathname}${location.search}`;

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.hasError) {
      auth.login(returnTo);
    }
  }, [auth, returnTo]);

  if (auth.hasError) {
    return <Navigate to="/unauthorized" replace />;
  }
  if (auth.isLoading || !auth.isAuthenticated) {
    return <CircularProgress aria-label="Authenticating" />;
  }
  return <Outlet />;
}
