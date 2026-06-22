import type { ReactElement } from 'react';
import { render, type RenderOptions, type RenderResult } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router-dom';
import { ConfigProvider } from '@/shared/config/config';
import { AuthContext, type AuthState } from '@/shared/auth/auth-context';
import { createStore } from '@/app/store';
import { TEST_CONFIG } from '@/testing/test-config';
import type { AppConfig } from '@/shared/config/app-config';
import { ThemedShell } from '@/testing/themed-shell';

const DEFAULT_AUTHENTICATED_STATE: AuthState = {
  isAuthenticated: true,
  isLoading: false,
  hasError: false,
  login: () => {},
  logout: () => {},
  userEmail: null,
};

export interface RenderWithProvidersOptions extends Omit<RenderOptions, 'wrapper'> {
  config?: AppConfig;
  route?: string;
  auth?: Partial<AuthState>;
}

export function renderWithProviders(ui: ReactElement, options: RenderWithProvidersOptions = {}): RenderResult {
  const { config = TEST_CONFIG, route = '/', auth, ...renderOptions } = options;
  const store = createStore(config);
  const authState: AuthState = { ...DEFAULT_AUTHENTICATED_STATE, ...auth };
  return render(
    <ThemedShell>
      <ConfigProvider config={config}>
        <Provider store={store}>
          <AuthContext.Provider value={authState}>
            <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
          </AuthContext.Provider>
        </Provider>
      </ConfigProvider>
    </ThemedShell>,
    renderOptions,
  );
}
