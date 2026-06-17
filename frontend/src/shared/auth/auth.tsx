import { createContext, useContext } from 'react';

const AuthContext = createContext<unknown>(null);

export function useAuth(): unknown {
  const auth = useContext(AuthContext);
  if (!auth) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return auth;
}
