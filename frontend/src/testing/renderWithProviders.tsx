import type { ReactElement, ReactNode } from 'react';
import { render, type RenderOptions, type RenderResult } from '@testing-library/react';
import { Provider } from 'react-redux';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { appTheme } from '@/shared/theme/theme';
import { ConfigProvider } from '@/shared/config/ConfigContext';
import { createStore } from '@/app/store';
import { TEST_CONFIG } from '@/testing/testConfig';
import type { AppConfig } from '@/shared/config/appConfig';

export function ThemedShell({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
}

export interface RenderWithProvidersOptions extends Omit<RenderOptions, 'wrapper'> {
  config?: AppConfig;
}

export function renderWithProviders(ui: ReactElement, options: RenderWithProvidersOptions = {}): RenderResult {
  const { config = TEST_CONFIG, ...renderOptions } = options;
  const store = createStore(config);
  return render(
    <ThemedShell>
      <ConfigProvider config={config}>
        <Provider store={store}>{ui}</Provider>
      </ConfigProvider>
    </ThemedShell>,
    renderOptions,
  );
}
