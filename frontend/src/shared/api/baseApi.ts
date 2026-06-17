import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import { API_TAGS } from '@/shared/api/apiTags';
import type { RootState } from '@/app/store';

const dynamicBaseQuery: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = (args, apiCtx, extra) => {
  const state = apiCtx.getState() as RootState;
  return fetchBaseQuery({
    baseUrl: state.config.apiBaseUrl,
    prepareHeaders: (headers) => {
      if (state.auth.accessToken) {
        headers.set('Authorization', `Bearer ${state.auth.accessToken}`);
      }
      return headers;
    },
  })(args, apiCtx, extra);
};

export const api = createApi({
  reducerPath: 'api',
  tagTypes: Object.values(API_TAGS),
  baseQuery: dynamicBaseQuery,
  endpoints: () => ({}),
});
