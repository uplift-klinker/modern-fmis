import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { clientsApi } from '@/features/clients/api/clients-api';
import { renderWithProviders } from '@/testing/render-with-providers';
import { TestingApiServer } from '@/testing/testing-api-server';
import { ModelFactory } from '@/testing/model-factory';

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
