import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from "@reduxjs/toolkit/query/react";
import { API_TAGS } from "@/shared/api/api-tags";
import type { RootState } from "@/app/store";

const dynamicBaseQuery: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = (
  args,
  apiCtx,
  extra,
) => {
  const { config } = apiCtx.getState() as RootState;
  return fetchBaseQuery({
    baseUrl: config.apiBaseUrl,
    prepareHeaders: (headers, { getState }) => {
      const { auth } = getState() as RootState;
      if (auth.accessToken) {
        headers.set("Authorization", `Bearer ${auth.accessToken}`);
      }
      return headers;
    },
  })(args, apiCtx, extra);
};

export const api = createApi({
  reducerPath: "api",
  tagTypes: Object.values(API_TAGS),
  baseQuery: dynamicBaseQuery,
  endpoints: () => ({}),
});
