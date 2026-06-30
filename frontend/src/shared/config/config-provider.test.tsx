import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ConfigProvider } from '@/shared/config/config-provider';
import { useConfig } from '@/shared/config/config-context';
import { TestingApiServer } from '@/testing/testing-api-server';
import { ModelFactory } from '@/testing/model-factory';
import { renderWithProviders } from '@/testing/render-with-providers';
import { ThemedShell } from '@/testing/themed-shell';

function ShowApiUrl() {
  return <div>api:{useConfig().apiBaseUrl}</div>;
}

function renderConfigProvider() {
  return render(
    <ThemedShell>
      <ConfigProvider>
        <ShowApiUrl />
      </ConfigProvider>
    </ThemedShell>,
  );
}

describe('ConfigProvider', () => {
  it('exposes the active config to consumers', () => {
    const config = ModelFactory.createAppConfig();
    renderWithProviders(<ShowApiUrl />, { config });
    expect(screen.getByText(`api:${config.apiBaseUrl}`)).toBeInTheDocument();
  });

  it('loads config.json itself and renders children once loaded', async () => {
    const config = TestingApiServer.setupConfig();
    renderConfigProvider();
    expect(await screen.findByText(`api:${config.apiBaseUrl}`)).toBeInTheDocument();
  });

  it('shows a loading indicator before the config resolves', () => {
    TestingApiServer.setupConfig({}, { delayMs: 50 });
    renderConfigProvider();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('shows an error fallback when the config fails to load', async () => {
    TestingApiServer.setupConfig({}, { status: 500 });
    renderConfigProvider();
    expect(await screen.findByRole('alert')).toHaveTextContent(/failed to load application configuration/i);
  });
});

describe('useConfig', () => {
  it('throws when used outside a ConfigProvider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() => render(<ShowApiUrl />)).toThrow(/within a ConfigProvider/);
    consoleError.mockRestore();
  });
});
