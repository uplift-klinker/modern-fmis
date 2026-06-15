# Frontend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the React/TypeScript frontend walking skeleton — the authenticated **Client UI** (list, create dialog, detail) wired to the existing backend — establishing the runtime-config, app-shell, routing/auth, RTK Query data layer, feature-slice, and centralized testing patterns every later frontend feature copies.

**Architecture:** Vite SPA. Production code is feature-sliced (`features/clients/…`); test code is centralized (`src/testing/`). The build is immutable; runtime settings come from a Zod-validated `config.json` fetched before render. Auth0 is wrapped behind our own `useAuth()` seam so it's testable. RTK Query is the data layer; React Router v7 with an auth guard that preserves deep-link `returnTo`. Tests are behavior-first (Testing Library role queries) and stub only the network edge with MSW via a config-aware `TestingApiServer`; data is built with a faker `ModelFactory`.

**Tech Stack:** React 19, Vite 8, TypeScript 6, Node 24 LTS + pnpm 11.7.0 (Corepack), MUI 9, Redux Toolkit 2 + RTK Query, React Router 7, `@auth0/auth0-react` 2, Zod 4, Vitest 4 + Testing Library + `@testing-library/jest-dom` + `@testing-library/user-event`, MSW 2, `@faker-js/faker` 10.

**Conventions (from `docs/conventions/`):** TDD — failing test first, every behavior ([`test-driven-development.md`](../../conventions/test-driven-development.md)); [`frontend-conventions.md`](../../conventions/frontend-conventions.md); no code comments; method names contain a verb; new commits only (no amend/force-push). Run frontend commands from `frontend/` via `zsh -lc`.

**Out of scope (deferred to the infra/Auth0 phase):** Playwright E2E and genuinely-live login + authenticated API calls (need a real Auth0 tenant). Farm/Field/activities/mapping UI, authorization.

---

## File Structure

```
frontend/
├─ .nvmrc                      24
├─ package.json                packageManager: pnpm@11.7.0; engines.node
├─ vite.config.ts              Vite + Vitest (jsdom, setupFiles)
├─ tsconfig*.json
├─ index.html
├─ public/config.json          local-dev default runtime config
├─ Dockerfile
└─ src/
   ├─ main.tsx                 bootstrap: load config → providers → router
   ├─ app/
   │  ├─ store.ts              configureStore + RTK Query api
   │  ├─ router.tsx            createBrowserRouter route table
   │  └─ App.tsx               provider composition (given a loaded config)
   ├─ shared/
   │  ├─ config/               appConfigSchema, loadConfig, ConfigProvider/useConfig
   │  ├─ auth/                  useAuth() seam (Auth0 in prod), AuthProvider, token holder
   │  └─ api/                   base RTK Query api (fetchBaseQuery + auth token)
   ├─ features/clients/
   │  ├─ clientSchemas.ts       Zod schemas + inferred types
   │  ├─ clientsApi.ts          RTK Query endpoints (injected)
   │  ├─ ClientsListPage.tsx
   │  ├─ CreateClientDialog.tsx
   │  ├─ ClientDetailPage.tsx
   │  └─ *.test.tsx             component tests (co-located with the feature)
   ├─ routes/                   WelcomePage, UnauthorizedPage, RequireAuth
   └─ testing/                  ALL test support
      ├─ setup.ts               jest-dom + MSW lifecycle
      ├─ testConfig.ts          the AppConfig used by tests (one base-URL source of truth)
      ├─ renderWithProviders.tsx
      ├─ requestCapture.ts      createRequestCapture / RequestCapture
      ├─ TestingApiServer.ts    MSW wrapper: start/reset/stop + per-endpoint setups
      └─ modelFactory.ts        ModelFactory (faker)
```

---

## Task 1: Scaffold the Vite app + test runner

**Files:** `frontend/` (generated), `frontend/.nvmrc`, `frontend/vite.config.ts`, `frontend/src/testing/setup.ts`, `frontend/src/smoke.test.ts`

- [ ] **Step 1: Scaffold and pin the toolchain**

From the repo root:

```bash
zsh -lc 'pnpm create vite@latest frontend --template react-ts'
cd frontend
printf '24\n' > .nvmrc
zsh -lc 'npm pkg set packageManager=pnpm@11.7.0'
zsh -lc 'npm pkg set engines.node=">=24 <25"'
zsh -lc 'pnpm install'
```

- [ ] **Step 2: Add the test toolchain**

```bash
cd frontend
zsh -lc 'pnpm add -D vitest@4 jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom'
```

- [ ] **Step 3: Configure Vitest in `vite.config.ts`**

`frontend/vite.config.ts`:

```ts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/testing/setup.ts'],
    css: false,
  },
});
```

- [ ] **Step 4: Create the test setup file**

`frontend/src/testing/setup.ts`:

```ts
import '@testing-library/jest-dom/vitest';
```

(The MSW lifecycle is added to this file in Task 5.)

- [ ] **Step 5: Write a smoke test (red → green)**

`frontend/src/smoke.test.ts`:

```ts
import { describe, it, expect } from 'vitest';

describe('toolchain', () => {
  it('runs vitest', () => {
    expect(1 + 1).toBe(2);
  });
});
```

Run: `zsh -lc 'pnpm vitest run'`
Expected: 1 passing test.

- [ ] **Step 6: Remove Vite template cruft we will not use**

```bash
cd frontend
rm -f src/App.css src/index.css src/assets/react.svg public/vite.svg
```

Leave `src/App.tsx`/`src/main.tsx` for now; they are replaced in later tasks.

- [ ] **Step 7: Commit**

```bash
git add frontend/ && git commit -m "Scaffold Vite React-TS frontend with Vitest, pin Node 24 + pnpm 11.7.0"
```

---

## Task 2: Install runtime dependencies + folder skeleton

**Files:** `frontend/package.json` (deps), empty dirs under `frontend/src/`

- [ ] **Step 1: Install runtime + remaining dev deps**

```bash
cd frontend
zsh -lc 'pnpm add react-router-dom@7 @reduxjs/toolkit@2 react-redux@9 @auth0/auth0-react@2 zod@4 @mui/material@9 @emotion/react @emotion/styled'
zsh -lc 'pnpm add -D msw@2 @faker-js/faker@10'
```

- [ ] **Step 2: Create the folder skeleton**

```bash
cd frontend/src
mkdir -p app shared/config shared/auth shared/api features/clients routes testing
```

- [ ] **Step 3: Verify the project still builds and tests pass**

Run: `zsh -lc 'pnpm vitest run && pnpm tsc --noEmit'`
Expected: smoke test passes; no type errors.

- [ ] **Step 4: Commit**

```bash
git add frontend/ && git commit -m "Add frontend runtime deps (MUI, RTK Query, Router, Auth0, Zod) and folder skeleton"
```

---

## Task 3: Runtime config (schema, loader, context)

**Files:** Create `frontend/src/shared/config/appConfig.ts`, `frontend/src/shared/config/ConfigContext.tsx`, `frontend/public/config.json`; Test `frontend/src/shared/config/appConfig.test.ts`

- [ ] **Step 1: Write the failing test**

`frontend/src/shared/config/appConfig.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { parseAppConfig } from './appConfig';

describe('parseAppConfig', () => {
  it('parses a valid config', () => {
    const config = parseAppConfig({
      apiBaseUrl: 'https://api.example.com',
      auth: { domain: 'tenant.auth0.com', clientId: 'abc', audience: 'https://api' },
    });
    expect(config.apiBaseUrl).toBe('https://api.example.com');
    expect(config.auth.domain).toBe('tenant.auth0.com');
  });

  it('throws when a required field is missing', () => {
    expect(() => parseAppConfig({ apiBaseUrl: 'https://api.example.com' })).toThrow();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/shared/config'`
Expected: FAIL — `./appConfig` does not exist.

- [ ] **Step 3: Implement the schema + parser**

`frontend/src/shared/config/appConfig.ts`:

```ts
import { z } from 'zod';

export const appConfigSchema = z.object({
  apiBaseUrl: z.string().min(1),
  auth: z.object({
    domain: z.string().min(1),
    clientId: z.string().min(1),
    audience: z.string().min(1),
  }),
});

export type AppConfig = z.infer<typeof appConfigSchema>;

export function parseAppConfig(value: unknown): AppConfig {
  return appConfigSchema.parse(value);
}

export async function loadAppConfig(): Promise<AppConfig> {
  const response = await fetch('/config.json', { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Failed to load config.json: ${response.status}`);
  }
  return parseAppConfig(await response.json());
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/shared/config'`
Expected: PASS (2 tests).

- [ ] **Step 5: Create the config context**

`frontend/src/shared/config/ConfigContext.tsx`:

```tsx
import { createContext, useContext, type ReactNode } from 'react';
import type { AppConfig } from './appConfig';

const ConfigContext = createContext<AppConfig | null>(null);

export function ConfigProvider({ config, children }: { config: AppConfig; children: ReactNode }) {
  return <ConfigContext.Provider value={config}>{children}</ConfigContext.Provider>;
}

export function useConfig(): AppConfig {
  const config = useContext(ConfigContext);
  if (!config) {
    throw new Error('useConfig must be used within a ConfigProvider');
  }
  return config;
}
```

- [ ] **Step 6: Create the local-dev default config**

`frontend/public/config.json`:

```json
{
  "apiBaseUrl": "http://localhost:8080",
  "auth": {
    "domain": "REPLACE_AT_DEPLOY.auth0.com",
    "clientId": "REPLACE_AT_DEPLOY",
    "audience": "https://api.modern-fmis"
  }
}
```

- [ ] **Step 7: Commit**

```bash
git add frontend/ && git commit -m "Add runtime config: Zod schema, loader, and ConfigProvider"
```

---

## Task 4: Auth seam, base API, and store

**Files:** Create `frontend/src/shared/auth/authToken.ts`, `frontend/src/shared/auth/useAuth.ts`, `frontend/src/shared/auth/AuthProvider.tsx`, `frontend/src/shared/api/baseApi.ts`, `frontend/src/app/store.ts`; Test `frontend/src/shared/auth/authToken.test.ts`

Auth0 is wrapped behind our own `useAuth()` so production uses Auth0 and tests inject a fake. The RTK Query base query reads a token via a small module-level holder that the auth layer keeps current.

- [ ] **Step 1: Write the failing test for the token holder**

`frontend/src/shared/auth/authToken.test.ts`:

```ts
import { describe, it, expect, beforeEach } from 'vitest';
import { setAccessTokenProvider, getAccessToken } from './authToken';

describe('access token holder', () => {
  beforeEach(() => setAccessTokenProvider(null));

  it('returns null when no provider is set', async () => {
    expect(await getAccessToken()).toBeNull();
  });

  it('returns the token from the registered provider', async () => {
    setAccessTokenProvider(async () => 'token-123');
    expect(await getAccessToken()).toBe('token-123');
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/shared/auth'`
Expected: FAIL — `./authToken` does not exist.

- [ ] **Step 3: Implement the token holder**

`frontend/src/shared/auth/authToken.ts`:

```ts
type AccessTokenProvider = () => Promise<string | null>;

let provider: AccessTokenProvider | null = null;

export function setAccessTokenProvider(next: AccessTokenProvider | null): void {
  provider = next;
}

export async function getAccessToken(): Promise<string | null> {
  return provider ? provider() : null;
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/shared/auth'`
Expected: PASS (2 tests).

- [ ] **Step 5: Create the `useAuth` seam and `AuthProvider`**

`frontend/src/shared/auth/useAuth.ts`:

```ts
import { createContext, useContext } from 'react';

export interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  hasError: boolean;
  userEmail: string | null;
  login: (returnTo: string) => void;
  logout: () => void;
}

export const AuthContext = createContext<AuthState | null>(null);

export function useAuth(): AuthState {
  const auth = useContext(AuthContext);
  if (!auth) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return auth;
}
```

`frontend/src/shared/auth/AuthProvider.tsx`:

```tsx
import { useEffect, type ReactNode } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { AuthContext, type AuthState } from './useAuth';
import { setAccessTokenProvider } from './authToken';

export function AuthProvider({ children }: { children: ReactNode }) {
  const auth0 = useAuth0();

  useEffect(() => {
    setAccessTokenProvider(auth0.isAuthenticated ? () => auth0.getAccessTokenSilently() : null);
    return () => setAccessTokenProvider(null);
  }, [auth0.isAuthenticated, auth0]);

  const value: AuthState = {
    isAuthenticated: auth0.isAuthenticated,
    isLoading: auth0.isLoading,
    hasError: auth0.error !== undefined,
    userEmail: auth0.user?.email ?? null,
    login: (returnTo: string) => auth0.loginWithRedirect({ appState: { returnTo } }),
    logout: () => auth0.logout({ logoutParams: { returnTo: window.location.origin } }),
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
```

- [ ] **Step 6: Create the base RTK Query API**

The `api` is an **eager module-load singleton** (so feature endpoints can `injectEndpoints` at import time without an init step). Its base URL is read at request time from a holder set via `setApiBaseUrl` — the bootstrap sets it from config, tests set it from `testConfig`.

`frontend/src/shared/api/baseApi.ts`:

```ts
import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import { getAccessToken } from '../auth/authToken';

let apiBaseUrl = '';

export function setApiBaseUrl(url: string): void {
  apiBaseUrl = url;
}

const dynamicBaseQuery: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = (args, store, extra) =>
  fetchBaseQuery({
    baseUrl: apiBaseUrl,
    prepareHeaders: async (headers) => {
      const token = await getAccessToken();
      if (token) {
        headers.set('Authorization', `Bearer ${token}`);
      }
      return headers;
    },
  })(args, store, extra);

export const api = createApi({
  reducerPath: 'api',
  tagTypes: ['Client'],
  baseQuery: dynamicBaseQuery,
  endpoints: () => ({}),
});
```

- [ ] **Step 7: Create the store factory**

`frontend/src/app/store.ts`:

```ts
import { configureStore } from '@reduxjs/toolkit';
import { api } from '../shared/api/baseApi';

export function createStore() {
  return configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  });
}

export type AppStore = ReturnType<typeof createStore>;
```

- [ ] **Step 8: Build + commit**

Run: `zsh -lc 'pnpm tsc --noEmit && pnpm vitest run src/shared'`
Expected: no type errors; auth token tests pass.

```bash
git add frontend/ && git commit -m "Add auth seam (useAuth/AuthProvider/token holder), base RTK Query api, and store factory"
```

---

## Task 5: Centralized testing harness

**Files:** Create `frontend/src/testing/testConfig.ts`, `requestCapture.ts`, `TestingApiServer.ts`, `renderWithProviders.tsx`; Modify `frontend/src/testing/setup.ts`; Test `frontend/src/testing/requestCapture.test.ts`

This harness is exercised by every feature test. The client-specific endpoint setups + `ModelFactory` come in Task 7 once the Client schemas exist; here we build the generic core.

- [ ] **Step 1: Write the failing test for `RequestCapture`**

`frontend/src/testing/requestCapture.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { createRequestCapture } from './requestCapture';

describe('createRequestCapture', () => {
  it('starts empty', () => {
    const capture = createRequestCapture<{ name: string }>();
    expect(capture.wasCalled).toBe(false);
    expect(capture.callCount).toBe(0);
    expect(capture.lastRequest).toBeUndefined();
  });

  it('records calls', () => {
    const capture = createRequestCapture<{ name: string }>();
    capture.record({ body: { name: 'Acme' }, headers: new Headers(), url: new URL('http://x/clients'), searchParams: new URLSearchParams() });
    expect(capture.wasCalled).toBe(true);
    expect(capture.callCount).toBe(1);
    expect(capture.lastRequest?.body.name).toBe('Acme');
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/testing/requestCapture'`
Expected: FAIL — `./requestCapture` does not exist.

- [ ] **Step 3: Implement `RequestCapture`**

`frontend/src/testing/requestCapture.ts`:

```ts
export interface CapturedRequest<TBody> {
  body: TBody;
  headers: Headers;
  url: URL;
  searchParams: URLSearchParams;
}

export interface RequestCapture<TBody> {
  readonly calls: ReadonlyArray<CapturedRequest<TBody>>;
  readonly lastRequest: CapturedRequest<TBody> | undefined;
  readonly wasCalled: boolean;
  readonly callCount: number;
  record(request: CapturedRequest<TBody>): void;
}

export function createRequestCapture<TBody>(): RequestCapture<TBody> {
  const calls: CapturedRequest<TBody>[] = [];
  return {
    get calls() { return calls; },
    get lastRequest() { return calls.at(-1); },
    get wasCalled() { return calls.length > 0; },
    get callCount() { return calls.length; },
    record(request) { calls.push(request); },
  };
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/testing/requestCapture'`
Expected: PASS (2 tests).

- [ ] **Step 5: Create the shared test config**

`frontend/src/testing/testConfig.ts`:

```ts
import type { AppConfig } from '../shared/config/appConfig';

export const testConfig: AppConfig = {
  apiBaseUrl: 'http://api.test',
  auth: { domain: 'test.auth0.com', clientId: 'test-client', audience: 'https://api.test' },
};
```

- [ ] **Step 6: Create the `TestingApiServer` core**

`frontend/src/testing/TestingApiServer.ts`:

```ts
import { setupServer } from 'msw/node';
import { http, HttpResponse, delay, type HttpHandler } from 'msw';
import { testConfig } from './testConfig';
import type { RequestCapture } from './requestCapture';

export interface SetupEndpointOptions<TBody = never> {
  delayMs?: number;
  status?: number;
  capture?: RequestCapture<TBody>;
}

const server = setupServer();

function url(path: string): string {
  return `${testConfig.apiBaseUrl}${path}`;
}

async function applyCommon<TBody>(
  request: Request,
  options: SetupEndpointOptions<TBody>,
): Promise<void> {
  if (options.delayMs) {
    await delay(options.delayMs);
  }
  if (options.capture) {
    const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
    const body = (hasBody ? await request.clone().json() : undefined) as TBody;
    options.capture.record({
      body,
      headers: request.headers,
      url: new URL(request.url),
      searchParams: new URL(request.url).searchParams,
    });
  }
}

export const TestingApiServer = {
  start(): void {
    server.listen({ onUnhandledRequest: 'error' });
  },
  reset(): void {
    server.resetHandlers();
  },
  stop(): void {
    server.close();
  },
  use(...handlers: HttpHandler[]): void {
    server.use(...handlers);
  },
  url,
  applyCommon,
  http,
  HttpResponse,
};
```

> Per-endpoint setups (`setupGetClientList`, etc.) are added in Task 7. `applyCommon` centralizes delay/capture so each endpoint setup stays tiny.

- [ ] **Step 7: Wire the MSW lifecycle into the global setup**

Replace `frontend/src/testing/setup.ts`:

```ts
import { afterAll, afterEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { TestingApiServer } from './TestingApiServer';

beforeAll(() => TestingApiServer.start());
afterEach(() => TestingApiServer.reset());
afterAll(() => TestingApiServer.stop());
```

- [ ] **Step 8: Create `renderWithProviders`**

Builds the real store (with the clients api injected), the config + a test auth context, MUI theme, and a memory router at a given route.

`frontend/src/testing/renderWithProviders.tsx`:

```tsx
import { type ReactElement, type ReactNode } from 'react';
import { render, type RenderResult } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { setApiBaseUrl } from '../shared/api/baseApi';
import { createStore } from '../app/store';
import { ConfigProvider } from '../shared/config/ConfigContext';
import { AuthContext, type AuthState } from '../shared/auth/useAuth';
import { setAccessTokenProvider } from '../shared/auth/authToken';
import { testConfig } from './testConfig';

const theme = createTheme();

export interface RenderOptions {
  route?: string;
  auth?: Partial<AuthState>;
}

const authenticatedDefault: AuthState = {
  isAuthenticated: true,
  isLoading: false,
  hasError: false,
  userEmail: 'tester@modern-fmis.test',
  login: () => {},
  logout: () => {},
};

export function renderWithProviders(ui: ReactElement, options: RenderOptions = {}): RenderResult {
  const auth: AuthState = { ...authenticatedDefault, ...options.auth };
  setAccessTokenProvider(auth.isAuthenticated ? async () => 'test-token' : null);

  setApiBaseUrl(testConfig.apiBaseUrl);
  const store = createStore();

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <ConfigProvider config={testConfig}>
        <AuthContext.Provider value={auth}>
          <Provider store={store}>
            <ThemeProvider theme={theme}>
              <MemoryRouter initialEntries={[options.route ?? '/']}>{children}</MemoryRouter>
            </ThemeProvider>
          </Provider>
        </AuthContext.Provider>
      </ConfigProvider>
    );
  }

  return render(ui, { wrapper: Wrapper });
}
```

> The `api` singleton already exists at import (Task 4), so `renderWithProviders` just sets its base URL and builds a fresh store (fresh RTK Query cache per test). Feature tests import `clientsApi` (Task 8), whose `injectEndpoints` registers its endpoints on that same singleton at import time — no init step, no ordering hazard.

- [ ] **Step 9: Run the suite + commit**

Run: `zsh -lc 'pnpm vitest run && pnpm tsc --noEmit'`
Expected: PASS (config, auth token, requestCapture). No type errors.

```bash
git add frontend/ && git commit -m "Add centralized testing harness: TestingApiServer (MSW), RequestCapture, renderWithProviders"
```

---

## Task 6: Client Zod schemas

**Files:** Create `frontend/src/features/clients/clientSchemas.ts`; Test `frontend/src/features/clients/clientSchemas.test.ts`

These mirror the backend `Models` (`ClientResponseModel`, `CreateClientRequestModel`, `ListResultModel<T>`) and carry the email-or-phone rule.

- [ ] **Step 1: Write the failing test**

`frontend/src/features/clients/clientSchemas.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { clientResponseSchema, createClientRequestSchema, clientListSchema } from './clientSchemas';

describe('client schemas', () => {
  it('parses a client response', () => {
    const parsed = clientResponseSchema.parse({
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Acme Farms',
      email: 'ops@acme.example',
      phoneNumber: null,
    });
    expect(parsed.name).toBe('Acme Farms');
  });

  it('accepts a create request with only a phone number', () => {
    expect(() => createClientRequestSchema.parse({ name: 'Acme', email: null, phoneNumber: '555-0100' })).not.toThrow();
  });

  it('rejects a create request with neither email nor phone', () => {
    expect(() => createClientRequestSchema.parse({ name: 'Acme', email: null, phoneNumber: null })).toThrow();
  });

  it('rejects a blank name', () => {
    expect(() => createClientRequestSchema.parse({ name: '', email: 'ops@acme.example', phoneNumber: null })).toThrow();
  });

  it('parses a list result', () => {
    const parsed = clientListSchema.parse({ items: [], totalCount: 0 });
    expect(parsed.totalCount).toBe(0);
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/clientSchemas'`
Expected: FAIL — `./clientSchemas` does not exist.

- [ ] **Step 3: Implement the schemas**

`frontend/src/features/clients/clientSchemas.ts`:

```ts
import { z } from 'zod';

export const clientResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  email: z.string().nullable(),
  phoneNumber: z.string().nullable(),
});

export const clientListSchema = z.object({
  items: z.array(clientResponseSchema),
  totalCount: z.number(),
});

export const createClientRequestObjectSchema = z.object({
  name: z.string().min(1),
  email: z.string().nullable(),
  phoneNumber: z.string().nullable(),
});

export const createClientRequestSchema = createClientRequestObjectSchema.refine(
  (value) => Boolean(value.email?.trim()) || Boolean(value.phoneNumber?.trim()),
  { message: 'Enter an email or a phone number.', path: ['contact'] },
);

export type ClientResponse = z.infer<typeof clientResponseSchema>;
export type ClientList = z.infer<typeof clientListSchema>;
export type CreateClientRequest = z.infer<typeof createClientRequestSchema>;
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/clientSchemas'`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add Client Zod schemas with the email-or-phone rule"
```

---

## Task 7: ModelFactory + client endpoint setups

**Files:** Create `frontend/src/testing/modelFactory.ts`; Modify `frontend/src/testing/TestingApiServer.ts`; Test `frontend/src/testing/modelFactory.test.ts`

- [ ] **Step 1: Write the failing test**

`frontend/src/testing/modelFactory.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { clientResponseSchema } from '../features/clients/clientSchemas';
import { ModelFactory } from './modelFactory';

describe('ModelFactory', () => {
  it('creates a valid client with defaults', () => {
    const client = ModelFactory.createClient();
    expect(() => clientResponseSchema.parse(client)).not.toThrow();
  });

  it('honors overrides', () => {
    const client = ModelFactory.createClient({ name: 'Acme Farms' });
    expect(client.name).toBe('Acme Farms');
  });

  it('creates a list of the requested size', () => {
    const list = ModelFactory.createClientList(3);
    expect(list.items).toHaveLength(3);
    expect(list.totalCount).toBe(3);
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/testing/modelFactory'`
Expected: FAIL — `./modelFactory` does not exist.

- [ ] **Step 3: Implement `ModelFactory`**

`frontend/src/testing/modelFactory.ts`:

```ts
import { faker } from '@faker-js/faker';
import type { ClientResponse, ClientList, CreateClientRequest } from '../features/clients/clientSchemas';

function createClient(overrides: Partial<ClientResponse> = {}): ClientResponse {
  return {
    id: faker.string.uuid(),
    name: faker.company.name(),
    email: faker.internet.email(),
    phoneNumber: faker.phone.number(),
    ...overrides,
  };
}

function createClientList(count = 2, overrides: Partial<ClientResponse>[] = []): ClientList {
  const items = Array.from({ length: count }, (_, i) => createClient(overrides[i]));
  return { items, totalCount: count };
}

function createClientRequest(overrides: Partial<CreateClientRequest> = {}): CreateClientRequest {
  return { name: faker.company.name(), email: faker.internet.email(), phoneNumber: null, ...overrides };
}

export const ModelFactory = { createClient, createClientList, createClientRequest };
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/testing/modelFactory'`
Expected: PASS (3 tests).

- [ ] **Step 5: Add the client endpoint setups to `TestingApiServer`**

Append to `frontend/src/testing/TestingApiServer.ts` — add these imports at the top and the methods to the exported object:

```ts
import type { ClientList, ClientResponse, CreateClientRequest } from '../features/clients/clientSchemas';
```

Add to the `TestingApiServer` object (alongside `start`/`reset`/`stop`):

```ts
  setupGetClientList(list: ClientList, options: SetupEndpointOptions = {}): void {
    server.use(
      http.get(url('/clients'), async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json(list, { status: options.status ?? 200 });
      }),
    );
  },
  setupGetClient(client: ClientResponse, options: SetupEndpointOptions = {}): void {
    server.use(
      http.get(url(`/clients/${client.id}`), async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json(client, { status: options.status ?? 200 });
      }),
    );
  },
  setupGetClientNotFound(id: string, options: SetupEndpointOptions = {}): void {
    server.use(
      http.get(url(`/clients/${id}`), async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json({ title: 'Not Found' }, { status: options.status ?? 404 });
      }),
    );
  },
  setupCreateClient(created: ClientResponse, options: SetupEndpointOptions<CreateClientRequest> = {}): void {
    server.use(
      http.post(url('/clients'), async ({ request }) => {
        await applyCommon(request, options);
        return HttpResponse.json(created, { status: options.status ?? 201 });
      }),
    );
  },
```

- [ ] **Step 6: Run the suite + commit**

Run: `zsh -lc 'pnpm vitest run && pnpm tsc --noEmit'`
Expected: all green.

```bash
git add frontend/ && git commit -m "Add faker ModelFactory and client endpoint setups on TestingApiServer"
```

---

## Task 8: Clients RTK Query API slice

**Files:** Create `frontend/src/features/clients/clientsApi.ts`; Test `frontend/src/features/clients/clientsApi.test.tsx`

The clients endpoints inject into the shared `api` singleton (Task 4) at import time — both the app and tests use the same registered endpoints. No init step (the base URL comes from `setApiBaseUrl`, which `renderWithProviders` and the bootstrap already call).

- [ ] **Step 1: Write the failing test (a hook-driven component through MSW)**

`frontend/src/features/clients/clientsApi.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { clientsApi } from './clientsApi';
import { renderWithProviders } from '../../testing/renderWithProviders';
import { TestingApiServer } from '../../testing/TestingApiServer';
import { ModelFactory } from '../../testing/modelFactory';

function Probe() {
  const { data } = clientsApi.useGetClientsQuery();
  return <div>{data ? `count:${data.totalCount}` : 'loading'}</div>;
}

describe('clientsApi', () => {
  it('fetches the client list', async () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(2));

    renderWithProviders(<Probe />);

    expect(await screen.findByText('count:2')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/clientsApi'`
Expected: FAIL — `./clientsApi` does not exist.

- [ ] **Step 3: Implement the clients api slice**

`frontend/src/features/clients/clientsApi.ts`:

```ts
import { api } from '../../shared/api/baseApi';
import type { ClientList, ClientResponse, CreateClientRequest } from './clientSchemas';

export const clientsApi = api.injectEndpoints({
  endpoints: (build) => ({
    getClients: build.query<ClientList, void>({
      query: () => '/clients',
      providesTags: ['Client'],
    }),
    getClient: build.query<ClientResponse, string>({
      query: (id) => `/clients/${id}`,
      providesTags: ['Client'],
    }),
    createClient: build.mutation<ClientResponse, CreateClientRequest>({
      query: (body) => ({ url: '/clients', method: 'POST', body }),
      invalidatesTags: ['Client'],
    }),
  }),
});
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/clientsApi && pnpm tsc --noEmit'`
Expected: PASS; no type errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add clients RTK Query api slice (get/list/create) injected on the shared api"
```

---

## Task 9: Clients list page

**Files:** Create `frontend/src/features/clients/ClientsListPage.tsx`; Test `frontend/src/features/clients/ClientsListPage.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/features/clients/ClientsListPage.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '../../testing/renderWithProviders';
import { TestingApiServer } from '../../testing/TestingApiServer';
import { ModelFactory } from '../../testing/modelFactory';
import { ClientsListPage } from './ClientsListPage';

describe('ClientsListPage', () => {
  it('shows each client name with contact sub-text', async () => {
    TestingApiServer.setupGetClientList({
      items: [ModelFactory.createClient({ name: 'Acme Farms', email: 'ops@acme.example', phoneNumber: '555-0100' })],
      totalCount: 1,
    });

    renderWithProviders(<ClientsListPage />, { route: '/clients' });

    expect(await screen.findByText('Acme Farms')).toBeInTheDocument();
    expect(screen.getByText(/ops@acme\.example/)).toBeInTheDocument();
  });

  it('shows a loading indicator while fetching', () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(1), { delayMs: 50 });
    renderWithProviders(<ClientsListPage />, { route: '/clients' });
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('shows an error state when the request fails', async () => {
    TestingApiServer.setupGetClientList(ModelFactory.createClientList(0), { status: 500 });
    renderWithProviders(<ClientsListPage />, { route: '/clients' });
    expect(await screen.findByText(/couldn’t load clients/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/ClientsListPage'`
Expected: FAIL — `./ClientsListPage` does not exist.

- [ ] **Step 3: Implement the page**

`frontend/src/features/clients/ClientsListPage.tsx`:

```tsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, List, ListItemButton, ListItemText, Stack, Typography } from '@mui/material';
import { clientsApi } from './clientsApi';
import { CreateClientDialog } from './CreateClientDialog';

export function ClientsListPage() {
  const navigate = useNavigate();
  const [createOpen, setCreateOpen] = useState(false);
  const { data, isLoading, isError } = clientsApi.useGetClientsQuery();

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5">Clients</Typography>
        <Button variant="contained" onClick={() => setCreateOpen(true)}>New Client</Button>
      </Stack>

      {isLoading && <CircularProgress aria-label="Loading clients" />}
      {isError && <Alert severity="error">We couldn’t load clients. Please try again.</Alert>}

      {data && (
        <List>
          {data.items.map((client) => (
            <ListItemButton key={client.id} onClick={() => navigate(`/clients/${client.id}`)}>
              <ListItemText
                primary={client.name}
                secondary={[client.email, client.phoneNumber].filter(Boolean).join(' · ') || '—'}
              />
            </ListItemButton>
          ))}
        </List>
      )}

      <CreateClientDialog open={createOpen} onClose={() => setCreateOpen(false)} />
    </Box>
  );
}
```

> `CreateClientDialog` is implemented in Task 10. To keep this task green on its own, create a temporary stub now and replace it in Task 10:
>
> `frontend/src/features/clients/CreateClientDialog.tsx`:
> ```tsx
> export function CreateClientDialog({ open }: { open: boolean; onClose: () => void }) {
>   return open ? null : null;
> }
> ```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/ClientsListPage'`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add ClientsListPage (list, loading, error) with a CreateClientDialog stub"
```

---

## Task 10: Create client dialog

**Files:** Replace `frontend/src/features/clients/CreateClientDialog.tsx`; Test `frontend/src/features/clients/CreateClientDialog.test.tsx`

- [ ] **Step 1: Write the failing tests**

`frontend/src/features/clients/CreateClientDialog.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '../../testing/renderWithProviders';
import { TestingApiServer } from '../../testing/TestingApiServer';
import { ModelFactory } from '../../testing/modelFactory';
import { createRequestCapture } from '../../testing/requestCapture';
import type { CreateClientRequest } from './clientSchemas';
import { CreateClientDialog } from './CreateClientDialog';

describe('CreateClientDialog', () => {
  it('blocks submit and shows an error when neither email nor phone is given', async () => {
    const user = userEvent.setup();
    const capture = createRequestCapture<CreateClientRequest>();
    TestingApiServer.setupCreateClient(ModelFactory.createClient(), { capture });

    renderWithProviders(<CreateClientDialog open onClose={() => {}} />, { route: '/clients' });

    await user.type(screen.getByLabelText(/name/i), 'Acme Farms');
    await user.click(screen.getByRole('button', { name: /create/i }));

    expect(await screen.findByText(/enter an email or a phone number/i)).toBeInTheDocument();
    expect(capture.wasCalled).toBe(false);
  });

  it('submits the entered values and the request carries them', async () => {
    const user = userEvent.setup();
    const capture = createRequestCapture<CreateClientRequest>();
    const created = ModelFactory.createClient({ name: 'Acme Farms' });
    TestingApiServer.setupCreateClient(created, { capture });

    renderWithProviders(<CreateClientDialog open onClose={() => {}} />, { route: '/clients' });

    await user.type(screen.getByLabelText(/name/i), 'Acme Farms');
    await user.type(screen.getByLabelText(/email/i), 'ops@acme.example');
    await user.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(capture.wasCalled).toBe(true));
    expect(capture.lastRequest?.body.name).toBe('Acme Farms');
    expect(capture.lastRequest?.body.email).toBe('ops@acme.example');
  });

  it('surfaces a server validation error (400)', async () => {
    const user = userEvent.setup();
    TestingApiServer.setupCreateClient(ModelFactory.createClient(), { status: 400 });

    renderWithProviders(<CreateClientDialog open onClose={() => {}} />, { route: '/clients' });

    await user.type(screen.getByLabelText(/name/i), 'Acme Farms');
    await user.type(screen.getByLabelText(/phone/i), '555-0100');
    await user.click(screen.getByRole('button', { name: /create/i }));

    expect(await screen.findByText(/couldn’t create the client/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/CreateClientDialog'`
Expected: FAIL — the stub renders nothing.

- [ ] **Step 3: Implement the dialog**

`frontend/src/features/clients/CreateClientDialog.tsx`:

```tsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { clientsApi } from './clientsApi';
import { createClientRequestSchema } from './clientSchemas';

export function CreateClientDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [createClient, { isLoading, isError }] = clientsApi.useCreateClientMutation();

  async function submit() {
    const parsed = createClientRequestSchema.safeParse({
      name,
      email: email || null,
      phoneNumber: phoneNumber || null,
    });
    if (!parsed.success) {
      setValidationError(parsed.error.issues[0]?.message ?? 'Please check the form.');
      return;
    }
    setValidationError(null);
    const result = await createClient(parsed.data);
    if ('data' in result) {
      onClose();
      navigate(`/clients/${result.data.id}`);
    }
  }

  return (
    <Dialog open={open} onClose={onClose}>
      <DialogTitle>New Client</DialogTitle>
      <DialogContent>
        <Stack spacing={2} mt={1}>
          {validationError && <Alert severity="warning">{validationError}</Alert>}
          {isError && <Alert severity="error">We couldn’t create the client. Please try again.</Alert>}
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} required />
          <TextField label="Email" value={email} onChange={(e) => setEmail(e.target.value)} />
          <TextField label="Phone" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={isLoading}>Create</Button>
      </DialogActions>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/CreateClientDialog'`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add CreateClientDialog (Zod validation, request capture, success navigation, 400 surfacing)"
```

---

## Task 11: Client detail page

**Files:** Create `frontend/src/features/clients/ClientDetailPage.tsx`; Test `frontend/src/features/clients/ClientDetailPage.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/features/clients/ClientDetailPage.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { renderWithProviders } from '../../testing/renderWithProviders';
import { TestingApiServer } from '../../testing/TestingApiServer';
import { ModelFactory } from '../../testing/modelFactory';
import { ClientDetailPage } from './ClientDetailPage';

function renderAt(id: string) {
  return renderWithProviders(
    <Routes><Route path="/clients/:id" element={<ClientDetailPage />} /></Routes>,
    { route: `/clients/${id}` },
  );
}

describe('ClientDetailPage', () => {
  it('shows the client details', async () => {
    const client = ModelFactory.createClient({ name: 'Acme Farms' });
    TestingApiServer.setupGetClient(client);
    renderAt(client.id);
    expect(await screen.findByRole('heading', { name: 'Acme Farms' })).toBeInTheDocument();
  });

  it('shows a not-found state for a missing client', async () => {
    const id = '00000000-0000-0000-0000-000000000000';
    TestingApiServer.setupGetClientNotFound(id);
    renderAt(id);
    expect(await screen.findByText(/client not found/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/ClientDetailPage'`
Expected: FAIL — `./ClientDetailPage` does not exist.

- [ ] **Step 3: Implement the page**

`frontend/src/features/clients/ClientDetailPage.tsx`:

```tsx
import { useParams } from 'react-router-dom';
import { Alert, Box, CircularProgress, Stack, Typography } from '@mui/material';
import { clientsApi } from './clientsApi';

export function ClientDetailPage() {
  const { id = '' } = useParams();
  const { data, isLoading, isError } = clientsApi.useGetClientQuery(id);

  if (isLoading) return <CircularProgress aria-label="Loading client" />;
  if (isError || !data) return <Alert severity="error">Client not found.</Alert>;

  return (
    <Box>
      <Typography variant="h5" component="h1">{data.name}</Typography>
      <Stack spacing={1} mt={2}>
        <Typography><strong>Email:</strong> {data.email ?? '—'}</Typography>
        <Typography><strong>Phone:</strong> {data.phoneNumber ?? '—'}</Typography>
      </Stack>
    </Box>
  );
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/features/clients/ClientDetailPage'`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add ClientDetailPage (details + not-found state)"
```

---

## Task 12: Auth guard, welcome, and unauthorized pages

**Files:** Create `frontend/src/routes/RequireAuth.tsx`, `frontend/src/routes/WelcomePage.tsx`, `frontend/src/routes/UnauthorizedPage.tsx`; Test `frontend/src/routes/RequireAuth.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/routes/RequireAuth.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { renderWithProviders } from '../testing/renderWithProviders';
import { RequireAuth } from './RequireAuth';

function Protected() {
  return (
    <Routes>
      <Route element={<RequireAuth />}>
        <Route path="/clients" element={<div>secret clients</div>} />
      </Route>
      <Route path="/unauthorized" element={<div>unauthorized page</div>} />
    </Routes>
  );
}

describe('RequireAuth', () => {
  it('renders the protected content when authenticated', () => {
    renderWithProviders(<Protected />, { route: '/clients', auth: { isAuthenticated: true } });
    expect(screen.getByText('secret clients')).toBeInTheDocument();
  });

  it('triggers login when unauthenticated', () => {
    const login = vi.fn();
    renderWithProviders(<Protected />, { route: '/clients', auth: { isAuthenticated: false, login } });
    expect(login).toHaveBeenCalledWith('/clients');
    expect(screen.queryByText('secret clients')).not.toBeInTheDocument();
  });

  it('redirects to /unauthorized on auth error', () => {
    renderWithProviders(<Protected />, { route: '/clients', auth: { isAuthenticated: false, hasError: true } });
    expect(screen.getByText('unauthorized page')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/routes/RequireAuth'`
Expected: FAIL — `./RequireAuth` does not exist.

- [ ] **Step 3: Implement the guard + pages**

`frontend/src/routes/RequireAuth.tsx`:

```tsx
import { useEffect } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { CircularProgress } from '@mui/material';
import { useAuth } from '../shared/auth/useAuth';

export function RequireAuth() {
  const auth = useAuth();
  const location = useLocation();
  const returnTo = `${location.pathname}${location.search}`;

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.hasError) {
      auth.login(returnTo);
    }
  }, [auth, returnTo]);

  if (auth.hasError) return <Navigate to="/unauthorized" replace />;
  if (auth.isLoading || !auth.isAuthenticated) return <CircularProgress aria-label="Authenticating" />;
  return <Outlet />;
}
```

`frontend/src/routes/WelcomePage.tsx`:

```tsx
import { Box, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { Link } from '@mui/material';

export function WelcomePage() {
  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>Welcome to modern-fmis</Typography>
      <Link component={RouterLink} to="/clients">Go to clients</Link>
    </Box>
  );
}
```

`frontend/src/routes/UnauthorizedPage.tsx`:

```tsx
import { Box, Typography } from '@mui/material';

export function UnauthorizedPage() {
  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>Unauthorized</Typography>
      <Typography>We couldn’t sign you in. Please contact your administrator.</Typography>
    </Box>
  );
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm vitest run src/routes/RequireAuth'`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add RequireAuth guard (login with returnTo, error→/unauthorized) and welcome/unauthorized pages"
```

---

## Task 13: Router and app shell

**Files:** Create `frontend/src/app/AppLayout.tsx`, `frontend/src/app/router.tsx`, `frontend/src/app/App.tsx`; Replace `frontend/src/main.tsx`; Test `frontend/src/app/router.test.tsx`

- [ ] **Step 1: Write the failing test (routing behavior with a memory router)**

`frontend/src/app/router.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '../testing/renderWithProviders';
import { appRoutes } from './router';
import { Routes } from 'react-router-dom';

describe('app routes', () => {
  it('redirects / to /welcome when authenticated', async () => {
    renderWithProviders(<Routes>{appRoutes}</Routes>, { route: '/', auth: { isAuthenticated: true } });
    expect(await screen.findByRole('heading', { name: /welcome to modern-fmis/i })).toBeInTheDocument();
  });

  it('serves the public /unauthorized route', () => {
    renderWithProviders(<Routes>{appRoutes}</Routes>, { route: '/unauthorized', auth: { isAuthenticated: false } });
    expect(screen.getByRole('heading', { name: /unauthorized/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm vitest run src/app/router'`
Expected: FAIL — `./router` does not exist.

- [ ] **Step 3: Create the layout, routes, and app**

`frontend/src/app/AppLayout.tsx`:

```tsx
import { AppBar, Box, Button, Container, Toolbar, Typography } from '@mui/material';
import { Outlet } from 'react-router-dom';
import { useAuth } from '../shared/auth/useAuth';

export function AppLayout() {
  const auth = useAuth();
  return (
    <Box>
      <AppBar position="static">
        <Toolbar sx={{ justifyContent: 'space-between' }}>
          <Typography variant="h6">modern-fmis</Typography>
          {auth.isAuthenticated && (
            <Button color="inherit" onClick={auth.logout}>{auth.userEmail ?? 'Log out'}</Button>
          )}
        </Toolbar>
      </AppBar>
      <Container sx={{ py: 3 }}><Outlet /></Container>
    </Box>
  );
}
```

`frontend/src/app/router.tsx`:

```tsx
import { Navigate, Route, createBrowserRouter, createRoutesFromElements } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { RequireAuth } from '../routes/RequireAuth';
import { WelcomePage } from '../routes/WelcomePage';
import { UnauthorizedPage } from '../routes/UnauthorizedPage';
import { ClientsListPage } from '../features/clients/ClientsListPage';
import { ClientDetailPage } from '../features/clients/ClientDetailPage';

export const appRoutes = (
  <>
    <Route path="/unauthorized" element={<UnauthorizedPage />} />
    <Route element={<RequireAuth />}>
      <Route element={<AppLayout />}>
        <Route path="/" element={<Navigate to="/welcome" replace />} />
        <Route path="/welcome" element={<WelcomePage />} />
        <Route path="/clients" element={<ClientsListPage />} />
        <Route path="/clients/:id" element={<ClientDetailPage />} />
      </Route>
    </Route>
  </>
);

export const router = createBrowserRouter(createRoutesFromElements(appRoutes));
```

`frontend/src/app/App.tsx`:

```tsx
import { Auth0Provider, type AppState } from '@auth0/auth0-react';
import { Provider } from 'react-redux';
import { RouterProvider } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { CssBaseline } from '@mui/material';
import type { AppConfig } from '../shared/config/appConfig';
import { ConfigProvider } from '../shared/config/ConfigContext';
import { AuthProvider } from '../shared/auth/AuthProvider';
import { setApiBaseUrl } from '../shared/api/baseApi';
import { createStore } from './store';
import { router } from './router';

const theme = createTheme();

export function App({ config }: { config: AppConfig }) {
  setApiBaseUrl(config.apiBaseUrl);
  const store = createStore();

  const onRedirectCallback = (appState?: AppState) => {
    router.navigate(appState?.returnTo ?? '/welcome');
  };

  return (
    <ConfigProvider config={config}>
      <Auth0Provider
        domain={config.auth.domain}
        clientId={config.auth.clientId}
        authorizationParams={{ audience: config.auth.audience, redirect_uri: window.location.origin }}
        onRedirectCallback={onRedirectCallback}
      >
        <AuthProvider>
          <Provider store={store}>
            <ThemeProvider theme={theme}>
              <CssBaseline />
              <RouterProvider router={router} />
            </ThemeProvider>
          </Provider>
        </AuthProvider>
      </Auth0Provider>
    </ConfigProvider>
  );
}
```

`frontend/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { loadAppConfig } from './shared/config/appConfig';
import { App } from './app/App';

async function bootstrap() {
  const root = createRoot(document.getElementById('root')!);
  try {
    const config = await loadAppConfig();
    root.render(<StrictMode><App config={config} /></StrictMode>);
  } catch (error) {
    root.render(<div role="alert">Failed to load application configuration.</div>);
    throw error;
  }
}

void bootstrap();
```

- [ ] **Step 4: Run it (green) + typecheck**

Run: `zsh -lc 'pnpm vitest run src/app/router && pnpm tsc --noEmit'`
Expected: PASS (2 tests); no type errors.

- [ ] **Step 5: Run the full suite**

Run: `zsh -lc 'pnpm vitest run'`
Expected: ALL pass.

- [ ] **Step 6: Commit**

```bash
git add frontend/ && git commit -m "Add app shell: layout, router (/, /welcome, /clients, /clients/:id, /unauthorized), Auth0 bootstrap with deep-link returnTo"
```

---

## Task 14: Zod ↔ OpenAPI contract test

**Files:** Create `frontend/src/features/clients/clientContract.test.ts`

This asserts the frontend Zod schemas still match the backend's published contract. It reads the OpenAPI document from a committed snapshot so the test is hermetic (no running backend); a follow-up can refresh the snapshot from `/openapi/v1.json`.

- [ ] **Step 1: Capture the backend OpenAPI snapshot**

With the backend running (`cd backend && zsh -lc 'dotnet run --project src/Fmis.Api'` in another shell, or via `docker compose up backend`), save the document:

```bash
cd /Users/bryceklinker/code/uplift-delivery/modern-fmis
zsh -lc 'curl -s http://localhost:8080/openapi/v1.json' > frontend/src/features/clients/openapi.snapshot.json
```

Stop the backend afterward.

- [ ] **Step 2: Write the contract test**

`frontend/src/features/clients/clientContract.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import openapi from './openapi.snapshot.json';
import { clientResponseSchema, createClientRequestObjectSchema } from './clientSchemas';

function propsOf(schemaName: string): string[] {
  const schema = (openapi as any).components?.schemas?.[schemaName];
  return Object.keys(schema?.properties ?? {});
}

describe('client contract matches the backend OpenAPI document', () => {
  it('ClientResponseModel properties match clientResponseSchema', () => {
    const apiProps = propsOf('ClientResponseModel').sort();
    const zodProps = Object.keys(clientResponseSchema.shape).sort();
    expect(zodProps).toEqual(apiProps);
  });

  it('CreateClientRequestModel properties match createClientRequestObjectSchema', () => {
    const apiProps = propsOf('CreateClientRequestModel').sort();
    const zodProps = Object.keys(createClientRequestObjectSchema.shape).sort();
    expect(zodProps).toEqual(apiProps);
  });
});
```

- [ ] **Step 3: Run it**

Run: `zsh -lc 'pnpm vitest run src/features/clients/clientContract'`
Expected: PASS — Zod property names equal the OpenAPI schema property names (`id/name/email/phoneNumber`; `name/email/phoneNumber`). If it fails, the schemas have drifted from the backend — fix the Zod schema (the backend is the source of truth).

- [ ] **Step 4: Commit**

```bash
git add frontend/ && git commit -m "Add Zod↔OpenAPI contract test against the backend schema snapshot"
```

---

## Task 15: Dockerfile and docker-compose frontend service

**Files:** Create `frontend/Dockerfile`, `frontend/nginx.conf`, `frontend/.dockerignore`; Modify `docker-compose.yml`

- [ ] **Step 1: Create the Dockerfile (build once, serve static)**

`frontend/Dockerfile`:

```dockerfile
FROM node:24-alpine AS build
WORKDIR /app
RUN corepack enable
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
RUN pnpm build

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
```

- [ ] **Step 2: Create the nginx SPA config**

`frontend/nginx.conf`:

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;
  location / {
    try_files $uri $uri/ /index.html;
  }
}
```

- [ ] **Step 3: Create `.dockerignore`**

`frontend/.dockerignore`:

```
node_modules
dist
```

- [ ] **Step 4: Add the frontend service to `docker-compose.yml`**

Add this service to the existing `docker-compose.yml` (which already has `db` and `backend`), under `services:`:

```yaml
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    ports:
      - "5173:80"
    depends_on:
      - backend
```

> The committed `public/config.json` (apiBaseUrl `http://localhost:8080`) is baked into the static assets for local dev. In deployed environments the `application` Pulumi stack replaces `config.json`. Note: full login won't complete locally until a real Auth0 tenant exists (infra phase).

- [ ] **Step 5: Build the image to verify it compiles + bundles**

```bash
cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/frontend
zsh -lc 'docker build -t fmis-frontend-test .'
```
Expected: build succeeds through the nginx stage. Then remove it: `zsh -lc 'docker rmi fmis-frontend-test'`.

- [ ] **Step 6: Commit**

```bash
git add frontend/Dockerfile frontend/nginx.conf frontend/.dockerignore docker-compose.yml
git commit -m "Add frontend Dockerfile (build once, nginx-served) and docker-compose service"
```

---

## Done criteria

- `cd frontend && zsh -lc 'pnpm vitest run'` passes all tests (config, auth token, requestCapture, modelFactory, client schemas, clientsApi, list/dialog/detail components, RequireAuth, router, contract).
- `pnpm tsc --noEmit` is clean; `pnpm build` produces `dist/`.
- The Client UI works against the backend contract: list (with loading/error), create dialog (Zod validation incl. email-or-phone, request carries the data, success → detail, 400 surfaced), detail (with not-found).
- Build-once/runtime-config: the app loads + Zod-validates `config.json` before render; RTK Query base URL + Auth0 settings come from it.
- Routing/auth: `/`→`/welcome`, guarded routes, `/unauthorized`, deep-link `returnTo` preserved; Auth0 wrapped behind the `useAuth()` seam.
- Centralized `src/testing/` harness in use everywhere: `renderWithProviders`, MSW-backed `TestingApiServer` (delay/status/capture), `RequestCapture`, faker `ModelFactory`. No snapshots, no self-mocking, role/label queries throughout.
- Patterns established for later features to copy: feature slice (`features/clients/`), RTK Query endpoints, the testing harness usage.
- **Deferred:** Playwright E2E + live integrated login (await the Auth0/infrastructure phase).
```
