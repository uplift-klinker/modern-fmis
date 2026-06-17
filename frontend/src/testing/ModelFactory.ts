import { faker } from '@faker-js/faker';
import { AppConfigSchema, type AppConfig } from '@/shared/config/appConfig';
import {
  ClientResponseSchema,
  ClientListSchema,
  type ClientResponse,
  type ClientList,
} from '@/features/clients/schemas/ClientSchemas';

export type AppConfigOverrides = Partial<Omit<AppConfig, 'auth'>> & {
  auth?: Partial<AppConfig['auth']>;
};

function createAppConfig(overrides: AppConfigOverrides = {}): AppConfig {
  const { auth: authOverrides, ...rest } = overrides;
  return AppConfigSchema.parse({
    apiBaseUrl: faker.internet.url(),
    ...rest,
    auth: {
      domain: faker.internet.domainName(),
      clientId: faker.string.alphanumeric(32),
      audience: faker.internet.url(),
      ...authOverrides,
    },
  });
}

function createClient(overrides: Partial<ClientResponse> = {}): ClientResponse {
  return ClientResponseSchema.parse({
    id: faker.string.uuid(),
    name: faker.company.name(),
    email: faker.internet.email(),
    phoneNumber: faker.phone.number(),
    ...overrides,
  });
}

function createClientList(count: number, template: Partial<ClientResponse> = {}): ClientList {
  return ClientListSchema.parse({
    items: Array.from({ length: count }, () => createClient(template)),
    totalCount: count,
  });
}

export const ModelFactory = {
  createAppConfig,
  createClient,
  createClientList,
};
