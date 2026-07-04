import { configureStore } from "@reduxjs/toolkit";
import { api } from "@/shared/api/base-api";
import { authReducer } from "@/shared/auth/auth-slice";
import type { AppConfig } from "@/shared/config/app-config";

export function createStore(config: AppConfig) {
  return configureStore({
    reducer: {
      [api.reducerPath]: api.reducer,
      auth: authReducer,
      config: () => config,
    },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
}

export type AppStore = ReturnType<typeof createStore>;
export type RootState = ReturnType<AppStore["getState"]>;
export type AppDispatch = AppStore["dispatch"];
