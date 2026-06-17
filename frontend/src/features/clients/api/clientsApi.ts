import { api } from '@/shared/api/baseApi';
import { API_TAGS } from '@/shared/api/apiTags';
import type { ClientList, ClientResponse } from '@/features/clients/schemas/ClientSchemas';

export const clientsApi = api.injectEndpoints({
  endpoints: (build) => ({
    getClients: build.query<ClientList, void>({
      query: () => '/clients',
      providesTags: [API_TAGS.Client],
    }),
    getClient: build.query<ClientResponse, string>({
      query: (id) => `/clients/${id}`,
      providesTags: [API_TAGS.Client],
    }),
  }),
});
