import { configureStore } from '@reduxjs/toolkit';
import { api } from '@/shared/api/baseApi';
import { authReducer } from '@/shared/auth/authSlice';
import type { AppConfig } from '@/shared/config/appConfig';

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
export type RootState = ReturnType<AppStore['getState']>;
export type AppDispatch = AppStore['dispatch'];
