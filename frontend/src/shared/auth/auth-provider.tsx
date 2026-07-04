import { useEffect, type ReactNode } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { useDispatch } from "react-redux";
import { setAccessToken } from "@/shared/auth/auth-slice";
import type { AppDispatch } from "@/app/store";
import { AuthContext, type AuthState } from "@/shared/auth/auth-context";

export function AuthProvider({ children }: { children: ReactNode }) {
  const auth0 = useAuth0();
  const dispatch = useDispatch<AppDispatch>();

  useEffect(() => {
    let active = true;
    if (auth0.isAuthenticated) {
      auth0.getAccessTokenSilently().then(
        (token) => active && dispatch(setAccessToken(token)),
        () => active && dispatch(setAccessToken(null)),
      );
    } else {
      dispatch(setAccessToken(null));
    }
    return () => {
      active = false;
    };
  }, [auth0.isAuthenticated, auth0, dispatch]);

  const value: AuthState = {
    isAuthenticated: auth0.isAuthenticated,
    isLoading: auth0.isLoading,
    hasError: auth0.error !== undefined,
    userEmail: auth0.user?.email ?? null,
    login: (returnTo) => auth0.loginWithRedirect({ appState: { returnTo } }),
    logout: () => auth0.logout({ logoutParams: { returnTo: window.location.origin } }),
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
