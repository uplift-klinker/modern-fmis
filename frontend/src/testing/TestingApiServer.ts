import { setupServer } from 'msw/node';
import { http, HttpResponse, delay, type HttpHandler } from 'msw';
import { ModelFactory, type AppConfigOverrides } from '@/testing/ModelFactory';
import { TEST_CONFIG } from '@/testing/testConfig';
import type { AppConfig } from '@/shared/config/appConfig';
import type { ClientList, ClientResponse, CreateClientRequest } from '@/features/clients/schemas/ClientSchemas';
import type { RequestCapture } from '@/testing/requestCapture';

export interface SetupEndpointOptions<TBody = never> {
  delayMs?: number;
  status?: number;
  capture?: RequestCapture<TBody>;
}

const server = setupServer();

function buildEndpointUrl(path: string): string {
  return `${TEST_CONFIG.apiBaseUrl}${path}`;
}

export async function applyCommon<TBody>(
  request: Request,
  options: SetupEndpointOptions<TBody>,
): Promise<void> {
  if (options.delayMs) {
    await delay(options.delayMs);
  }
  if (options.capture) {
    const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
    const body = (hasBody ? await request.clone().json() : undefined) as TBody;
    const url = new URL(request.url);
    options.capture.record({
      body,
      headers: request.headers,
      url,
      searchParams: url.searchParams,
    });
  }
}

export const TestingApiServer = {
  start: () => server.listen({ onUnhandledRequest: 'error' }),
  reset: () => server.resetHandlers(),
  stop: () => server.close(),
  use: (...handlers: HttpHandler[]) => server.use(...handlers),
  setupConfig(
    overrides: AppConfigOverrides = {},
    options: SetupEndpointOptions = {},
  ): AppConfig {
    const config = ModelFactory.createAppConfig(overrides);
    server.use(
      http.get('/config.json', async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json(config, { status: options.status ?? 200 });
      }),
    );
    return config;
  },
  setupGetClientList(list: ClientList, options: SetupEndpointOptions = {}) {
    server.use(
      http.get(buildEndpointUrl('/clients'), async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json(list, { status: options.status ?? 200 });
      }),
    );
  },
  setupGetClient(client: ClientResponse, options: SetupEndpointOptions = {}) {
    server.use(
      http.get(buildEndpointUrl(`/clients/${client.id}`), async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json(client, { status: options.status ?? 200 });
      }),
    );
  },
  setupCreateClient(created: ClientResponse, options: SetupEndpointOptions<CreateClientRequest> = {}) {
    server.use(
      http.post(buildEndpointUrl('/clients'), async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json(created, { status: options.status ?? 201 });
      }),
    );
  },
};
