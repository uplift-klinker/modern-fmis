import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { renderWithProviders } from '@/testing/render-with-providers';
import { TestingApiServer } from '@/testing/testing-api-server';
import { ModelFactory } from '@/testing/model-factory';
import { ClientDetailPage } from '@/features/clients/pages/client-detail-page';

describe('ClientDetailPage', () => {
  it('shows the client identified by the route param', async () => {
    const client = ModelFactory.createClient({ name: 'Acme Farms' });
    TestingApiServer.setupGetClient(client);

    renderWithProviders(
      <Routes>
        <Route path="/clients/:id" element={<ClientDetailPage />} />
      </Routes>,
      { route: `/clients/${client.id}` },
    );

    expect(await screen.findByRole('heading', { name: 'Acme Farms' })).toBeInTheDocument();
  });
});
