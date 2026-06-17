import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Routes, Route, useParams } from 'react-router-dom';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/ModelFactory';
import { ClientsListPage } from '@/features/clients/pages/ClientsListPage';

function DetailMarker() {
  const { id } = useParams();
  return <div>detail of {id}</div>;
}

describe('ClientsListPage', () => {
  it('keeps the list visible and renders the selected client in the detail pane', async () => {
    TestingApiServer.setupGetClientList({
      items: [ModelFactory.createClient({ name: 'Acme Farms' })],
      totalCount: 1,
    });

    renderWithProviders(
      <Routes>
        <Route path="/clients" element={<ClientsListPage />}>
          <Route path=":id" element={<DetailMarker />} />
        </Route>
      </Routes>,
      { route: '/clients' },
    );
    await userEvent.click(await screen.findByText('Acme Farms'));

    expect(await screen.findByText(/detail of/i)).toBeInTheDocument();
    expect(screen.getByText('Acme Farms')).toBeInTheDocument();
  });

  it('creates a client from the dialog and navigates to its detail', async () => {
    const created = ModelFactory.createClient({ name: 'New Farm' });
    TestingApiServer.setupGetClientList({ items: [], totalCount: 0 });
    TestingApiServer.setupCreateClient(created);

    renderWithProviders(
      <Routes>
        <Route path="/clients" element={<ClientsListPage />}>
          <Route path=":id" element={<DetailMarker />} />
        </Route>
      </Routes>,
      { route: '/clients' },
    );

    await userEvent.click(await screen.findByRole('button', { name: /new client/i }));
    await userEvent.type(screen.getByLabelText(/name/i), 'New Farm');
    await userEvent.type(screen.getByLabelText(/email/i), 'hi@newfarm.example');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByText(`detail of ${created.id}`)).toBeInTheDocument();
  });
});
