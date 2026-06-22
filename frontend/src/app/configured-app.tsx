import { useMemo } from 'react';
import { Auth0Provider, type AppState } from '@auth0/auth0-react';
import { Provider } from 'react-redux';
import { RouterProvider } from 'react-router-dom';
import { useConfig } from '@/shared/config/config-context';
import { AuthProvider } from '@/shared/auth/auth-provider';
import { createStore } from '@/app/store';
import { router } from '@/app/router';

export function ConfiguredApp() {
  const config = useConfig();
  const store = useMemo(() => createStore(config), [config]);

  const onRedirectCallback = (appState?: AppState) => {
    void router.navigate(appState?.returnTo ?? '/welcome');
  };

  return (
    <Provider store={store}>
      <Auth0Provider
        domain={config.auth.domain}
        clientId={config.auth.clientId}
        authorizationParams={{ audience: config.auth.audience, redirect_uri: window.location.origin }}
        onRedirectCallback={onRedirectCallback}
      >
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </Auth0Provider>
    </Provider>
  );
}
