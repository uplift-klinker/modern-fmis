import { createContext, useContext } from 'react';

export interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  hasError: boolean;
  login: (returnTo: string) => void;
}

export const AuthContext = createContext<AuthState | null>(null);

export function useAuth(): AuthState {
  const auth = useContext(AuthContext);
  if (!auth) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return auth;
}
