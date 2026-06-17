import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Routes, Route } from 'react-router-dom';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/ModelFactory';
import { ClientsListPage } from '@/features/clients/pages/ClientsListPage';

describe('ClientsListPage', () => {
  it('keeps the list visible and renders the selected client in the detail pane', async () => {
    TestingApiServer.setupGetClientList({
      items: [ModelFactory.createClient({ name: 'Acme Farms' })],
      totalCount: 1,
    });
    renderWithProviders(
      <Routes>
        <Route path="/clients" element={<ClientsListPage />}>
          <Route path=":id" element={<div>client detail</div>} />
        </Route>
      </Routes>,
      { route: '/clients' },
    );
    await userEvent.click(await screen.findByText('Acme Farms'));
    expect(await screen.findByText('client detail')).toBeInTheDocument();
    expect(screen.getByText('Acme Farms')).toBeInTheDocument();
  });
});
