import type { Page } from '@playwright/test';
import { e2eConfig } from './config';

const SCOPE = 'openid profile email';

export interface E2eToken {
  accessToken: string;
  idToken: string;
  expiresIn: number;
  scope: string;
}

export async function generateToken(): Promise<E2eToken> {
  const config = e2eConfig();
  const response = await fetch(`https://${config.authDomain}/oauth/token`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      grant_type: 'http://auth0.com/oauth/grant-type/password-realm',
      realm: 'Username-Password-Authentication',
      client_id: config.e2eClientId,
      client_secret: config.clientSecret,
      username: config.username,
      password: config.password,
      audience: config.audience,
      scope: SCOPE,
    }),
  });
  if (!response.ok) {
    throw new Error(`Auth0 token request failed: ${response.status} ${await response.text()}`);
  }
  const body = (await response.json()) as {
    access_token: string;
    id_token: string;
    expires_in: number;
    scope?: string;
  };
  return {
    accessToken: body.access_token,
    idToken: body.id_token,
    expiresIn: body.expires_in,
    scope: body.scope ?? SCOPE,
  };
}

export async function interactiveLogin(page: Page): Promise<void> {
  const config = e2eConfig();
  await page.goto('/');
  await page.getByLabel('Email address').fill(config.username);
  await page.getByLabel('Password').fill(config.password);
  await page.getByRole('button', { name: 'Continue', exact: false }).click();
  await page.waitForURL('**/welcome');
}

function decodeJwtPayload(token: string): Record<string, unknown> {
  const payload = token.split('.')[1];
  const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
  const json = Buffer.from(normalized, 'base64').toString('utf8');
  return JSON.parse(json) as Record<string, unknown>;
}

export async function seedAuthSession(page: Page, token: E2eToken): Promise<void> {
  const config = e2eConfig();
  const claims = decodeJwtPayload(token.idToken);
  const key = ['@@auth0spajs@@', config.clientId, config.audience, token.scope]
    .filter(Boolean)
    .join('::');
  const entry = {
    body: {
      access_token: token.accessToken,
      id_token: token.idToken,
      token_type: 'Bearer',
      expires_in: token.expiresIn,
      audience: config.audience,
      scope: token.scope,
      client_id: config.clientId,
      decodedToken: { claims, user: claims },
    },
    expiresAt: Math.floor(Date.now() / 1000) + token.expiresIn,
  };
  await page.addInitScript(
    ([storageKey, storageValue]) => {
      window.localStorage.setItem(storageKey, storageValue);
    },
    [key, JSON.stringify(entry)] as const,
  );
}
