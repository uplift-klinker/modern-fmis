import { createContext, useContext, useEffect, type ReactNode } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { useDispatch } from 'react-redux';
import { setAccessToken } from '@/shared/auth/authSlice';
import type { AppDispatch } from '@/app/store';

export interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  hasError: boolean;
  login: (returnTo: string) => void;
  logout: () => void;
  userEmail: string | null;
}

export const AuthContext = createContext<AuthState | null>(null);

export function useAuth(): AuthState {
  const auth = useContext(AuthContext);
  if (!auth) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return auth;
}

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
