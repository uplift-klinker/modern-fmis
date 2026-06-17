import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/ModelFactory';
import { ClientsListPage } from '@/features/clients/pages/ClientsListPage';

describe('ClientsListPage', () => {
  it('shows each client name with contact sub-text', async () => {
    TestingApiServer.setupGetClientList({
      items: [ModelFactory.createClient({ name: 'Acme Farms', email: 'ops@acme.example', phoneNumber: '555-0100' })],
      totalCount: 1,
    });
    renderWithProviders(<ClientsListPage />);
    expect(await screen.findByText('Acme Farms')).toBeInTheDocument();
    expect(screen.getByText(/ops@acme\.example/)).toBeInTheDocument();
  });

  it('shows a loading indicator while fetching', () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(1), { delayMs: 50 });
    renderWithProviders(<ClientsListPage />);
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('shows an error state when the request fails', async () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(0), { status: 500 });
    renderWithProviders(<ClientsListPage />);
    expect(await screen.findByText(/couldn't load clients/i)).toBeInTheDocument();
  });
});
