import { api } from "@/shared/api/base-api";
import { API_TAGS } from "@/shared/api/api-tags";
import type {
  ClientList,
  ClientResponse,
  CreateClientRequest,
} from "@/features/clients/schemas/client-schemas";

export const clientsApi = api.injectEndpoints({
  endpoints: (build) => ({
    getClients: build.query<ClientList, void>({
      query: () => "/clients",
      providesTags: [API_TAGS.Client],
    }),
    getClient: build.query<ClientResponse, string>({
      query: (id) => `/clients/${id}`,
      providesTags: [API_TAGS.Client],
    }),
    createClient: build.mutation<ClientResponse, CreateClientRequest>({
      query: (body) => ({ url: "/clients", method: "POST", body }),
      invalidatesTags: [API_TAGS.Client],
    }),
  }),
});
