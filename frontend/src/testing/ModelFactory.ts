import { faker } from '@faker-js/faker';
import { AppConfigSchema, type AppConfig } from '@/shared/config/appConfig';

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

export const ModelFactory = {
  createAppConfig,
};
