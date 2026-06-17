import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { clientsApi } from '@/features/clients/api/clientsApi';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/ModelFactory';

function Probe() {
  const { data } = clientsApi.useGetClientsQuery();
  return <div>{data ? `count:${data.totalCount}` : 'loading'}</div>;
}

describe('clientsApi', () => {
  it('fetches the client list', async () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(2));

    renderWithProviders(<Probe />);

    expect(await screen.findByText('count:2')).toBeInTheDocument();
  });
});
