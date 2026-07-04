import { test, expect } from '@playwright/test';
import { generateToken } from '../support/auth';
import { authorizedRequest } from '../support/api';

test('a generated Auth0 token is accepted by the deployed backend', async () => {
  const token = await generateToken();
  const api = await authorizedRequest(token.accessToken);

  const response = await api.get('/clients');

  expect(response.status()).toBe(200);
  await api.dispose();
});
