import type { ReactElement, ReactNode } from 'react';
import { render, type RenderOptions, type RenderResult } from '@testing-library/react';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { appTheme } from '@/shared/theme/theme';
import { ConfigProvider } from '@/shared/config/ConfigContext';
import { ModelFactory } from '@/testing/ModelFactory';
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
  const { config = ModelFactory.createAppConfig(), ...renderOptions } = options;
  return render(
    <ThemedShell>
      <ConfigProvider config={config}>{ui}</ConfigProvider>
    </ThemedShell>,
    renderOptions,
  );
}
