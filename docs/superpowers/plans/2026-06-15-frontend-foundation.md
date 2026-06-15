# Frontend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the React/TypeScript frontend walking skeleton — the authenticated **Client UI** (list, create dialog, detail) wired to the existing backend — establishing the runtime-config, store/auth, RTK Query data layer, TanStack-Form, feature-slice, and centralized testing patterns every later frontend feature copies.

**Architecture:** Vite SPA. Production code is feature-sliced (`features/<f>/{api,schemas,pages,dialogs,components}`); test code is centralized (`src/testing/`). Imports use a `@/` → `src/` alias (test support via `@/testing/…`). The build is immutable; the `ConfigProvider` loads a Zod-validated `config.json` at runtime. There are **no global mutable holders**: the API base URL and the Auth0 access token flow through the Redux store — `createStore(config)` seeds a `config` slice, and the `AuthProvider` (mounted inside Redux) dispatches the token into an `auth` slice; the RTK Query `baseQuery` reads both from state. React Router v7 with an auth guard that preserves deep-link `returnTo`. Forms use TanStack Form validated by the slice's Zod schema. Tests are behavior-first and stub only the network edge with MSW via a config-aware `TestingApiServer`; data is built with a faker `ModelFactory`.

**Tech Stack:** React 19, Vite 8, TypeScript 6, Node 24 LTS + pnpm 11.7.0 (Corepack), MUI 9, Redux Toolkit 2 + RTK Query, React Router 7, `@auth0/auth0-react` 2, `@tanstack/react-form` 1, Zod 4, Vitest 4 + Testing Library + `@testing-library/jest-dom` + `@testing-library/user-event`, MSW 2, `@faker-js/faker` 10.

**Conventions:** TDD — failing test first, every behavior ([`test-driven-development.md`](../../conventions/test-driven-development.md)); [`frontend-conventions.md`](../../conventions/frontend-conventions.md) (PascalCase schemas, no parse wrappers, `UPPER_SNAKE_CASE` constants, `API_TAGS`, `safeParse` failure tests, config/token-through-store, feature subfolders, `@/` aliases); no code comments; method names contain a verb; new commits only. Run frontend commands from `frontend/` via `zsh -lc`.

**Out of scope (deferred to the infra/Auth0 phase):** Playwright E2E and live login + authenticated API calls (need a real Auth0 tenant). Farm/Field/activities/mapping UI, authorization.

---

## File Structure

```
frontend/
├─ .nvmrc · package.json (packageManager pnpm@11.7.0, engines.node) · index.html
├─ vite.config.ts (Vite + Vitest + @/ alias) · tsconfig.app.json (paths)
├─ public/config.json · Dockerfile · nginx.conf · .dockerignore
└─ src/
   ├─ main.tsx                     trivial: render <App/>
   ├─ app/
   │  ├─ App.tsx                   <ConfigProvider><ConfiguredApp/></ConfigProvider>
   │  ├─ ConfiguredApp.tsx         useConfig → store + Auth0Provider + AuthProvider + router
   │  ├─ AppLayout.tsx · router.tsx · store.ts
   ├─ shared/
   │  ├─ config/ appConfig.ts (AppConfigSchema, loadAppConfig) · ConfigContext.tsx (ConfigProvider/useConfig)
   │  ├─ auth/   authSlice.ts · auth.tsx (AuthContext/useAuth/AuthProvider)
   │  └─ api/    apiTags.ts (API_TAGS) · baseApi.ts (api singleton, state-driven baseQuery)
   ├─ features/clients/
   │  ├─ schemas/  ClientSchemas.ts (+ .test.ts)
   │  ├─ api/      clientsApi.ts (+ .test.tsx)
   │  ├─ pages/    ClientsListPage.tsx · ClientDetailPage.tsx (+ tests)
   │  └─ dialogs/  CreateClientDialog.tsx (+ test)
   ├─ routes/      RequireAuth.tsx · WelcomePage.tsx · UnauthorizedPage.tsx (+ RequireAuth test)
   └─ testing/     setup.ts · testConfig.ts (TEST_CONFIG) · requestCapture.ts · TestingApiServer.ts
                   · modelFactory.ts · renderWithProviders.tsx
```

---

## Task 1: Scaffold the app, test runner, and `@/` alias

**Files:** `frontend/` (generated), `.nvmrc`, `vite.config.ts`, `tsconfig.app.json`, `src/testing/setup.ts`, `src/smoke.test.ts`

- [ ] **Step 1: Scaffold + pin the toolchain**

```bash
zsh -lc 'pnpm create vite@latest frontend --template react-ts'
cd frontend
printf '24\n' > .nvmrc
zsh -lc 'npm pkg set packageManager=pnpm@11.7.0'
zsh -lc 'npm pkg set engines.node=">=24 <25"'
zsh -lc 'pnpm add -D vitest@4 jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom'
zsh -lc 'npm pkg set scripts.test="vitest run"'
zsh -lc 'npm pkg set scripts.typecheck="tsc -p tsconfig.app.json --noEmit"'
zsh -lc 'pnpm install'
```

All later commands use these scripts — `pnpm test [path]` and `pnpm typecheck` — never raw `pnpm vitest`/`pnpm tsc`. (`pnpm dev`, `pnpm build`, `pnpm lint` come from the Vite template.)

- [ ] **Step 2: Configure Vitest + the `@/` alias in `vite.config.ts`**

`frontend/vite.config.ts`:

```ts
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  test: { environment: 'jsdom', globals: true, setupFiles: ['./src/testing/setup.ts'], css: false },
});
```

- [ ] **Step 3: Add the path alias to TypeScript**

In `frontend/tsconfig.app.json` (the project that type-checks `src/`), add to `compilerOptions`:

```json
"paths": { "@/*": ["./src/*"] }
```

> No `baseUrl` — it's deprecated in TypeScript 6 (`TS5101`) and unnecessary; TS resolves `paths` relative to this tsconfig's own directory (`frontend/`), so `@/*` → `frontend/src/*`. Type-check via `pnpm typecheck` (it targets `tsconfig.app.json` so the paths resolve).

- [ ] **Step 4: Create the test setup file**

`frontend/src/testing/setup.ts`:

```ts
import '@testing-library/jest-dom/vitest';
```

(The MSW lifecycle is added in Task 6.)

- [ ] **Step 5: Smoke test (red → green)**

`frontend/src/smoke.test.ts`:

```ts
import { describe, it, expect } from 'vitest';

describe('toolchain', () => {
  it('runs vitest', () => {
    expect(1 + 1).toBe(2);
  });
});
```

Run: `zsh -lc 'pnpm test'` → 1 passing test.

- [ ] **Step 6: Strip the template to a clean, buildable shell**

Delete the template `App`, its styles, and the asset folder so nothing imports removed files, and render a placeholder from `main.tsx` (the real `App` is built in Task 14). Remove whatever asset files the installed Vite template generated:

```bash
cd frontend && rm -rf src/App.tsx src/App.css src/index.css src/assets
```

Replace `frontend/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

createRoot(document.getElementById('root')!).render(<StrictMode><div>modern-fmis</div></StrictMode>);
```

After this, `pnpm typecheck` must pass (no imports of removed files) and `pnpm build` must succeed.

- [ ] **Step 7: Commit**

```bash
git add frontend/ && git commit -m "Scaffold Vite React-TS frontend (Vitest, @/ alias, Node 24 + pnpm 11.7.0)"
```

---

## Task 2: Dependencies + folder skeleton

**Files:** `frontend/package.json`, empty dirs under `src/`

- [ ] **Step 1: Install runtime + remaining dev deps**

```bash
cd frontend
zsh -lc 'pnpm add react-router-dom@7 @reduxjs/toolkit@2 react-redux@9 @auth0/auth0-react@2 @tanstack/react-form@1 zod@4 @mui/material@9 @emotion/react @emotion/styled'
zsh -lc 'pnpm add -D msw@2 @faker-js/faker@10'
```

- [ ] **Step 2: Approve dependency build scripts (pnpm 11)**

pnpm 11 blocks dependency postinstall/build scripts by default and reports `ERR_PNPM_IGNORED_BUILDS` for `browser-tabs-lock` (an Auth0 dep) and `msw`. Approve them declaratively (the committed equivalent of `pnpm approve-builds`) in `frontend/pnpm-workspace.yaml`:

```yaml
allowBuilds:
  browser-tabs-lock: true
  msw: true
```

If `pnpm` left a now-ignored `pnpm` field in `package.json`, remove it (`npm pkg delete pnpm`). Then `zsh -lc 'pnpm install'` runs the approved postinstalls — confirm no `ERR_PNPM_IGNORED_BUILDS`.

- [ ] **Step 3: Create the folder skeleton**

```bash
cd frontend/src
mkdir -p app shared/config shared/auth shared/api \
  features/clients/schemas features/clients/api features/clients/pages features/clients/dialogs features/clients/components \
  routes testing
```

- [ ] **Step 4: Verify**

Run: `zsh -lc 'pnpm test && pnpm typecheck'` → smoke passes; no type errors.

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add frontend deps (MUI, RTK Query, Router, Auth0, TanStack Form, Zod), approve builds, feature/test folders"
```

---

## Task 3: Runtime config (schema, loader, self-loading provider)

**Files:** Create `src/shared/config/appConfig.ts`, `src/shared/config/ConfigContext.tsx`, `public/config.json`; Test `src/shared/config/appConfig.test.ts`

- [ ] **Step 1: Write the failing test (using `safeParse`)**

`frontend/src/shared/config/appConfig.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { AppConfigSchema } from '@/shared/config/appConfig';

describe('AppConfigSchema', () => {
  it('parses a valid config', () => {
    const result = AppConfigSchema.safeParse({
      apiBaseUrl: 'https://api.example.com',
      auth: { domain: 'tenant.auth0.com', clientId: 'abc', audience: 'https://api' },
    });
    expect(result.success).toBe(true);
  });

  it('fails when a required field is missing', () => {
    const result = AppConfigSchema.safeParse({ apiBaseUrl: 'https://api.example.com' });
    expect(result.success).toBe(false);
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm test src/shared/config'` → FAIL (module missing).

- [ ] **Step 3: Implement the schema + loader (no parse wrapper)**

`frontend/src/shared/config/appConfig.ts`:

```ts
import { z } from 'zod';

export const AppConfigSchema = z.object({
  apiBaseUrl: z.string().min(1),
  auth: z.object({
    domain: z.string().min(1),
    clientId: z.string().min(1),
    audience: z.string().min(1),
  }),
});

export type AppConfig = z.infer<typeof AppConfigSchema>;

export async function loadAppConfig(): Promise<AppConfig> {
  const response = await fetch('/config.json', { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Failed to load config.json: ${response.status}`);
  }
  return AppConfigSchema.parse(await response.json());
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm test src/shared/config'` → PASS (2).

- [ ] **Step 5: Self-loading `ConfigProvider` + `useConfig`**

`frontend/src/shared/config/ConfigContext.tsx`:

```tsx
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { CircularProgress } from '@mui/material';
import { loadAppConfig, type AppConfig } from '@/shared/config/appConfig';

const ConfigContext = createContext<AppConfig | null>(null);

export function ConfigProvider({ config, children }: { config?: AppConfig; children: ReactNode }) {
  const [loaded, setLoaded] = useState<AppConfig | null>(config ?? null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (config) return;
    let active = true;
    loadAppConfig().then(
      (value) => active && setLoaded(value),
      () => active && setFailed(true),
    );
    return () => { active = false; };
  }, [config]);

  if (failed) return <div role="alert">Failed to load application configuration.</div>;
  if (!loaded) return <CircularProgress aria-label="Loading configuration" />;
  return <ConfigContext.Provider value={loaded}>{children}</ConfigContext.Provider>;
}

export function useConfig(): AppConfig {
  const config = useContext(ConfigContext);
  if (!config) {
    throw new Error('useConfig must be used within a ConfigProvider');
  }
  return config;
}
```

- [ ] **Step 6: Local-dev default config**

`frontend/public/config.json`:

```json
{
  "apiBaseUrl": "http://localhost:8080",
  "auth": { "domain": "REPLACE_AT_DEPLOY.auth0.com", "clientId": "REPLACE_AT_DEPLOY", "audience": "https://api.modern-fmis" }
}
```

- [ ] **Step 7: Commit**

```bash
git add frontend/ && git commit -m "Add runtime config: AppConfigSchema, loadAppConfig, self-loading ConfigProvider"
```

---

## Task 4: API tags, store slices, base API, and store

**Files:** Create `src/shared/api/apiTags.ts`, `src/shared/auth/authSlice.ts`, `src/shared/api/baseApi.ts`, `src/app/store.ts`; Test `src/shared/auth/authSlice.test.ts`

- [ ] **Step 1: Write the failing test for the auth slice**

`frontend/src/shared/auth/authSlice.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { authReducer, setAccessToken } from '@/shared/auth/authSlice';

describe('authSlice', () => {
  it('starts with no token', () => {
    expect(authReducer(undefined, { type: '@@init' }).accessToken).toBeNull();
  });

  it('stores a token', () => {
    const state = authReducer(undefined, setAccessToken('token-123'));
    expect(state.accessToken).toBe('token-123');
  });

  it('clears the token', () => {
    const state = authReducer({ accessToken: 'old' }, setAccessToken(null));
    expect(state.accessToken).toBeNull();
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm test src/shared/auth/authSlice'` → FAIL.

- [ ] **Step 3: Implement `API_TAGS`**

`frontend/src/shared/api/apiTags.ts`:

```ts
export const API_TAGS = { Client: 'Client' } as const;

export type ApiTag = (typeof API_TAGS)[keyof typeof API_TAGS];
```

- [ ] **Step 4: Implement the auth slice**

`frontend/src/shared/auth/authSlice.ts`:

```ts
import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

interface AuthSliceState {
  accessToken: string | null;
}

const initialState: AuthSliceState = { accessToken: null };

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setAccessToken: (state, action: PayloadAction<string | null>) => {
      state.accessToken = action.payload;
    },
  },
});

export const { setAccessToken } = authSlice.actions;
export const authReducer = authSlice.reducer;
```

- [ ] **Step 5: Implement the base API (base URL + token read from state)**

`frontend/src/shared/api/baseApi.ts`:

```ts
import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import { API_TAGS } from '@/shared/api/apiTags';
import type { RootState } from '@/app/store';

const dynamicBaseQuery: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = (args, apiCtx, extra) => {
  const state = apiCtx.getState() as RootState;
  return fetchBaseQuery({
    baseUrl: state.config.apiBaseUrl,
    prepareHeaders: (headers) => {
      if (state.auth.accessToken) {
        headers.set('Authorization', `Bearer ${state.auth.accessToken}`);
      }
      return headers;
    },
  })(args, apiCtx, extra);
};

export const api = createApi({
  reducerPath: 'api',
  tagTypes: Object.values(API_TAGS),
  baseQuery: dynamicBaseQuery,
  endpoints: () => ({}),
});
```

- [ ] **Step 6: Implement the store factory (config passed in)**

`frontend/src/app/store.ts`:

```ts
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
```

- [ ] **Step 7: Run it (green) + typecheck**

Run: `zsh -lc 'pnpm test src/shared/auth/authSlice && pnpm typecheck'` → PASS; no type errors. (`baseApi` imports `RootState` as a type only, so the `store ↔ baseApi` reference is erased at runtime.)

- [ ] **Step 8: Commit**

```bash
git add frontend/ && git commit -m "Add API_TAGS, auth slice, state-driven base API, and store(config) factory"
```

---

## Task 5: Auth seam (single file, dispatches token into the store)

**Files:** Create `src/shared/auth/auth.tsx`

The seam is one file: `AuthContext`, `useAuth`, and `AuthProvider`. `AuthProvider` runs inside the Redux `Provider`, reads Auth0, and dispatches the access token (and refreshes) into the store. Tests inject an `AuthContext` value directly (no module mocking), so this file has no standalone test — it's exercised by component/route tests via the injected context.

- [ ] **Step 1: Implement the seam**

`frontend/src/shared/auth/auth.tsx`:

```tsx
import { createContext, useContext, useEffect, type ReactNode } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { useDispatch } from 'react-redux';
import { setAccessToken } from '@/shared/auth/authSlice';
import type { AppDispatch } from '@/app/store';

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

export function AuthProvider({ children }: { children: ReactNode }) {
  const auth0 = useAuth0();
  const dispatch = useDispatch<AppDispatch>();

  useEffect(() => {
    let active = true;
    if (auth0.isAuthenticated) {
      auth0.getAccessTokenSilently().then(
        (token) => active && dispatch(setAccessToken(token)),
        () => active && dispatch(setAccessToken(null)),
      );
    } else {
      dispatch(setAccessToken(null));
    }
    return () => { active = false; };
  }, [auth0.isAuthenticated, auth0, dispatch]);

  const value: AuthState = {
    isAuthenticated: auth0.isAuthenticated,
    isLoading: auth0.isLoading,
    hasError: auth0.error !== undefined,
    userEmail: auth0.user?.email ?? null,
    login: (returnTo) => auth0.loginWithRedirect({ appState: { returnTo } }),
    logout: () => auth0.logout({ logoutParams: { returnTo: window.location.origin } }),
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
```

> Token refresh: `getAccessTokenSilently()` refreshes transparently; this effect re-dispatches on auth change. A proactive refresh (interval / on-expiry) is wired when live Auth0 is integrated (infra phase).

- [ ] **Step 2: Typecheck + commit**

Run: `zsh -lc 'pnpm typecheck'`

```bash
git add frontend/ && git commit -m "Add auth seam (useAuth/AuthProvider) that dispatches the token into the store"
```

---

## Task 6: Centralized testing harness

**Files:** Create `src/testing/testConfig.ts`, `requestCapture.ts`, `TestingApiServer.ts`, `renderWithProviders.tsx`; Modify `src/testing/setup.ts`; Test `src/testing/requestCapture.test.ts`

- [ ] **Step 1: Write the failing test for `RequestCapture`**

`frontend/src/testing/requestCapture.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { RequestCapture } from '@/testing/requestCapture';

describe('RequestCapture', () => {
  it('starts empty', () => {
    const capture = new RequestCapture<{ name: string }>();
    expect(capture.wasCalled).toBe(false);
    expect(capture.callCount).toBe(0);
    expect(capture.lastRequest).toBeUndefined();
  });

  it('records calls', () => {
    const capture = new RequestCapture<{ name: string }>();
    capture.record({ body: { name: 'Acme' }, headers: new Headers(), url: new URL('http://x/clients'), searchParams: new URLSearchParams() });
    expect(capture.wasCalled).toBe(true);
    expect(capture.callCount).toBe(1);
    expect(capture.lastRequest?.body.name).toBe('Acme');
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm test src/testing/requestCapture'` → FAIL.

- [ ] **Step 3: Implement `RequestCapture` as a class**

`frontend/src/testing/requestCapture.ts`:

```ts
export interface CapturedRequest<TBody> {
  body: TBody;
  headers: Headers;
  url: URL;
  searchParams: URLSearchParams;
}

export class RequestCapture<TBody> {
  private readonly recorded: CapturedRequest<TBody>[] = [];

  get calls(): ReadonlyArray<CapturedRequest<TBody>> { return this.recorded; }
  get lastRequest(): CapturedRequest<TBody> | undefined { return this.recorded.at(-1); }
  get wasCalled(): boolean { return this.recorded.length > 0; }
  get callCount(): number { return this.recorded.length; }

  record(request: CapturedRequest<TBody>): void {
    this.recorded.push(request);
  }
}
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm test src/testing/requestCapture'` → PASS (2).

- [ ] **Step 5: Shared test config constant**

`frontend/src/testing/testConfig.ts`:

```ts
import type { AppConfig } from '@/shared/config/appConfig';

export const TEST_CONFIG: AppConfig = {
  apiBaseUrl: 'http://api.test',
  auth: { domain: 'test.auth0.com', clientId: 'test-client', audience: 'https://api.test' },
};
```

- [ ] **Step 6: `TestingApiServer` core**

`frontend/src/testing/TestingApiServer.ts`:

```ts
import { setupServer } from 'msw/node';
import { http, HttpResponse, delay, type HttpHandler } from 'msw';
import { TEST_CONFIG } from '@/testing/testConfig';
import type { RequestCapture } from '@/testing/requestCapture';

export interface SetupEndpointOptions<TBody = never> {
  delayMs?: number;
  status?: number;
  capture?: RequestCapture<TBody>;
}

const server = setupServer();

export function endpointUrl(path: string): string {
  return `${TEST_CONFIG.apiBaseUrl}${path}`;
}

export async function applyCommon<TBody>(request: Request, options: SetupEndpointOptions<TBody>): Promise<void> {
  if (options.delayMs) {
    await delay(options.delayMs);
  }
  if (options.capture) {
    const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
    const body = (hasBody ? await request.clone().json() : undefined) as TBody;
    const url = new URL(request.url);
    options.capture.record({ body, headers: request.headers, url, searchParams: url.searchParams });
  }
}

export const TestingApiServer = {
  start: () => server.listen({ onUnhandledRequest: 'error' }),
  reset: () => server.resetHandlers(),
  stop: () => server.close(),
  use: (...handlers: HttpHandler[]) => server.use(...handlers),
};
```

> Per-endpoint setups (`setupGetClientList`, etc.) are added in Task 8; `endpointUrl`/`applyCommon` keep each one tiny.

- [ ] **Step 7: Wire MSW lifecycle into the global setup**

Replace `frontend/src/testing/setup.ts`:

```ts
import { afterAll, afterEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { TestingApiServer } from '@/testing/TestingApiServer';

beforeAll(() => TestingApiServer.start());
afterEach(() => TestingApiServer.reset());
afterAll(() => TestingApiServer.stop());
```

- [ ] **Step 8: `renderWithProviders`**

`frontend/src/testing/renderWithProviders.tsx`:

```tsx
import { type ReactElement, type ReactNode } from 'react';
import { render, type RenderResult } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { createStore } from '@/app/store';
import { setAccessToken } from '@/shared/auth/authSlice';
import { ConfigProvider } from '@/shared/config/ConfigContext';
import { AuthContext, type AuthState } from '@/shared/auth/auth';
import { TEST_CONFIG } from '@/testing/testConfig';

const theme = createTheme();

const DEFAULT_AUTHENTICATED_STATE: AuthState = {
  isAuthenticated: true,
  isLoading: false,
  hasError: false,
  userEmail: 'tester@modern-fmis.test',
  login: () => {},
  logout: () => {},
};

export interface RenderOptions {
  route?: string;
  auth?: Partial<AuthState>;
}

export function renderWithProviders(ui: ReactElement, options: RenderOptions = {}): RenderResult {
  const auth: AuthState = { ...DEFAULT_AUTHENTICATED_STATE, ...options.auth };
  const store = createStore(TEST_CONFIG);
  store.dispatch(setAccessToken(auth.isAuthenticated ? 'test-token' : null));

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <ConfigProvider config={TEST_CONFIG}>
        <Provider store={store}>
          <AuthContext.Provider value={auth}>
            <ThemeProvider theme={theme}>
              <MemoryRouter initialEntries={[options.route ?? '/']}>{children}</MemoryRouter>
            </ThemeProvider>
          </AuthContext.Provider>
        </Provider>
      </ConfigProvider>
    );
  }

  return render(ui, { wrapper: Wrapper });
}
```

- [ ] **Step 9: Run the suite + commit**

Run: `zsh -lc 'pnpm test && pnpm typecheck'` → green.

```bash
git add frontend/ && git commit -m "Add testing harness: TestingApiServer (MSW), RequestCapture class, renderWithProviders"
```

---

## Task 7: Client Zod schemas

**Files:** Create `src/features/clients/schemas/ClientSchemas.ts`; Test `src/features/clients/schemas/ClientSchemas.test.ts`

- [ ] **Step 1: Write the failing test (`safeParse` for failure cases)**

`frontend/src/features/clients/schemas/ClientSchemas.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { ClientResponseSchema, CreateClientRequestSchema, ClientListSchema } from '@/features/clients/schemas/ClientSchemas';

describe('client schemas', () => {
  it('parses a client response', () => {
    const parsed = ClientResponseSchema.parse({
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Acme Farms',
      email: 'ops@acme.example',
      phoneNumber: null,
    });
    expect(parsed.name).toBe('Acme Farms');
  });

  it('accepts a create request with only a phone number', () => {
    expect(CreateClientRequestSchema.safeParse({ name: 'Acme', email: null, phoneNumber: '555-0100' }).success).toBe(true);
  });

  it('rejects a create request with neither email nor phone', () => {
    expect(CreateClientRequestSchema.safeParse({ name: 'Acme', email: null, phoneNumber: null }).success).toBe(false);
  });

  it('rejects a blank name', () => {
    expect(CreateClientRequestSchema.safeParse({ name: '', email: 'ops@acme.example', phoneNumber: null }).success).toBe(false);
  });

  it('parses a list result', () => {
    expect(ClientListSchema.safeParse({ items: [], totalCount: 0 }).success).toBe(true);
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm test src/features/clients/schemas'` → FAIL.

- [ ] **Step 3: Implement the schemas (PascalCase, object split before refine)**

`frontend/src/features/clients/schemas/ClientSchemas.ts`:

```ts
import { z } from 'zod';

export const ClientResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  email: z.string().nullable(),
  phoneNumber: z.string().nullable(),
});

export const ClientListSchema = z.object({
  items: z.array(ClientResponseSchema),
  totalCount: z.number(),
});

export const CreateClientRequestObjectSchema = z.object({
  name: z.string().min(1),
  email: z.string().nullable(),
  phoneNumber: z.string().nullable(),
});

export const CreateClientRequestSchema = CreateClientRequestObjectSchema.refine(
  (value) => Boolean(value.email?.trim()) || Boolean(value.phoneNumber?.trim()),
  { message: 'Enter an email or a phone number.', path: ['contact'] },
);

export type ClientResponse = z.infer<typeof ClientResponseSchema>;
export type ClientList = z.infer<typeof ClientListSchema>;
export type CreateClientRequest = z.infer<typeof CreateClientRequestSchema>;
```

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'pnpm test src/features/clients/schemas'` → PASS (5).

```bash
git add frontend/ && git commit -m "Add Client Zod schemas (PascalCase) with the email-or-phone rule"
```

---

## Task 8: ModelFactory + client endpoint setups

**Files:** Create `src/testing/modelFactory.ts`; Modify `src/testing/TestingApiServer.ts`; Test `src/testing/modelFactory.test.ts`

- [ ] **Step 1: Write the failing test**

`frontend/src/testing/modelFactory.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { ClientResponseSchema } from '@/features/clients/schemas/ClientSchemas';
import { ModelFactory } from '@/testing/modelFactory';

describe('ModelFactory', () => {
  it('creates a valid client with defaults', () => {
    expect(ClientResponseSchema.safeParse(ModelFactory.createClient()).success).toBe(true);
  });

  it('honors overrides', () => {
    expect(ModelFactory.createClient({ name: 'Acme Farms' }).name).toBe('Acme Farms');
  });

  it('creates a list of the requested size', () => {
    const list = ModelFactory.createClientList(3);
    expect(list.items).toHaveLength(3);
    expect(list.totalCount).toBe(3);
  });
});
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'pnpm test src/testing/modelFactory'` → FAIL.

- [ ] **Step 3: Implement `ModelFactory`**

`frontend/src/testing/modelFactory.ts`:

```ts
import { faker } from '@faker-js/faker';
import type { ClientResponse, ClientList, CreateClientRequest } from '@/features/clients/schemas/ClientSchemas';

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
  return { items: Array.from({ length: count }, (_, i) => createClient(overrides[i])), totalCount: count };
}

function createClientRequest(overrides: Partial<CreateClientRequest> = {}): CreateClientRequest {
  return { name: faker.company.name(), email: faker.internet.email(), phoneNumber: null, ...overrides };
}

export const ModelFactory = { createClient, createClientList, createClientRequest };
```

- [ ] **Step 4: Run it (green)**

Run: `zsh -lc 'pnpm test src/testing/modelFactory'` → PASS (3).

- [ ] **Step 5: Add client endpoint setups to `TestingApiServer`**

At the top of `frontend/src/testing/TestingApiServer.ts` add:

```ts
import type { ClientList, ClientResponse, CreateClientRequest } from '@/features/clients/schemas/ClientSchemas';
```

Add these methods to the exported `TestingApiServer` object (alongside `start`/`reset`/`stop`/`use`):

```ts
  setupGetClientList(list: ClientList, options: SetupEndpointOptions = {}) {
    server.use(http.get(endpointUrl('/clients'), async ({ request }) => {
      await applyCommon(request, options);
      return HttpResponse.json(list, { status: options.status ?? 200 });
    }));
  },
  setupGetClient(client: ClientResponse, options: SetupEndpointOptions = {}) {
    server.use(http.get(endpointUrl(`/clients/${client.id}`), async ({ request }) => {
      await applyCommon(request, options);
      return HttpResponse.json(client, { status: options.status ?? 200 });
    }));
  },
  setupGetClientNotFound(id: string, options: SetupEndpointOptions = {}) {
    server.use(http.get(endpointUrl(`/clients/${id}`), async ({ request }) => {
      await applyCommon(request, options);
      return HttpResponse.json({ title: 'Not Found' }, { status: options.status ?? 404 });
    }));
  },
  setupCreateClient(created: ClientResponse, options: SetupEndpointOptions<CreateClientRequest> = {}) {
    server.use(http.post(endpointUrl('/clients'), async ({ request }) => {
      await applyCommon(request, options);
      return HttpResponse.json(created, { status: options.status ?? 201 });
    }));
  },
```

- [ ] **Step 6: Run the suite + commit**

Run: `zsh -lc 'pnpm test && pnpm typecheck'` → green.

```bash
git add frontend/ && git commit -m "Add faker ModelFactory and client endpoint setups on TestingApiServer"
```

---

## Task 9: Clients RTK Query API slice

**Files:** Create `src/features/clients/api/clientsApi.ts`; Test `src/features/clients/api/clientsApi.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/features/clients/api/clientsApi.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { clientsApi } from '@/features/clients/api/clientsApi';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/modelFactory';

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

Run: `zsh -lc 'pnpm test src/features/clients/api'` → FAIL.

- [ ] **Step 3: Implement the slice (inject into the singleton, `API_TAGS`)**

`frontend/src/features/clients/api/clientsApi.ts`:

```ts
import { api } from '@/shared/api/baseApi';
import { API_TAGS } from '@/shared/api/apiTags';
import type { ClientList, ClientResponse, CreateClientRequest } from '@/features/clients/schemas/ClientSchemas';

export const clientsApi = api.injectEndpoints({
  endpoints: (build) => ({
    getClients: build.query<ClientList, void>({
      query: () => '/clients',
      providesTags: [API_TAGS.Client],
    }),
    getClient: build.query<ClientResponse, string>({
      query: (id) => `/clients/${id}`,
      providesTags: [API_TAGS.Client],
    }),
    createClient: build.mutation<ClientResponse, CreateClientRequest>({
      query: (body) => ({ url: '/clients', method: 'POST', body }),
      invalidatesTags: [API_TAGS.Client],
    }),
  }),
});
```

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'pnpm test src/features/clients/api && pnpm typecheck'` → PASS.

```bash
git add frontend/ && git commit -m "Add clients RTK Query api slice (get/list/create) using API_TAGS"
```

---

## Task 10: Clients list page

**Files:** Create `src/features/clients/pages/ClientsListPage.tsx`; Test `…/ClientsListPage.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/features/clients/pages/ClientsListPage.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/modelFactory';
import { ClientsListPage } from '@/features/clients/pages/ClientsListPage';

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

Run: `zsh -lc 'pnpm test src/features/clients/pages/ClientsListPage'` → FAIL.

- [ ] **Step 3: Implement the page (with a CreateClientDialog stub)**

`frontend/src/features/clients/pages/ClientsListPage.tsx`:

```tsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, List, ListItemButton, ListItemText, Stack, Typography } from '@mui/material';
import { clientsApi } from '@/features/clients/api/clientsApi';
import { CreateClientDialog } from '@/features/clients/dialogs/CreateClientDialog';

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

Create a temporary stub (replaced in Task 11) so this task is green:

`frontend/src/features/clients/dialogs/CreateClientDialog.tsx`:

```tsx
export function CreateClientDialog(_: { open: boolean; onClose: () => void }) {
  return null;
}
```

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'pnpm test src/features/clients/pages/ClientsListPage'` → PASS (3).

```bash
git add frontend/ && git commit -m "Add ClientsListPage (list, loading, error) with a CreateClientDialog stub"
```

---

## Task 11: Create client dialog (TanStack Form)

**Files:** Replace `src/features/clients/dialogs/CreateClientDialog.tsx`; Test `…/CreateClientDialog.test.tsx`

- [ ] **Step 1: Write the failing tests**

`frontend/src/features/clients/dialogs/CreateClientDialog.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/modelFactory';
import { RequestCapture } from '@/testing/requestCapture';
import type { CreateClientRequest } from '@/features/clients/schemas/ClientSchemas';
import { CreateClientDialog } from '@/features/clients/dialogs/CreateClientDialog';

describe('CreateClientDialog', () => {
  it('blocks submit and shows an error when neither email nor phone is given', async () => {
    const user = userEvent.setup();
    const capture = new RequestCapture<CreateClientRequest>();
    TestingApiServer.setupCreateClient(ModelFactory.createClient(), { capture });

    renderWithProviders(<CreateClientDialog open onClose={() => {}} />, { route: '/clients' });

    await user.type(screen.getByLabelText(/name/i), 'Acme Farms');
    await user.click(screen.getByRole('button', { name: /create/i }));

    expect(await screen.findByText(/enter an email or a phone number/i)).toBeInTheDocument();
    expect(capture.wasCalled).toBe(false);
  });

  it('submits the entered values; the request carries them', async () => {
    const user = userEvent.setup();
    const capture = new RequestCapture<CreateClientRequest>();
    TestingApiServer.setupCreateClient(ModelFactory.createClient({ name: 'Acme Farms' }), { capture });

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

Run: `zsh -lc 'pnpm test src/features/clients/dialogs/CreateClientDialog'` → FAIL (stub renders nothing).

- [ ] **Step 3: Implement the dialog with TanStack Form**

`frontend/src/features/clients/dialogs/CreateClientDialog.tsx`:

```tsx
import { useForm } from '@tanstack/react-form';
import { useNavigate } from 'react-router-dom';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { clientsApi } from '@/features/clients/api/clientsApi';
import { CreateClientRequestSchema } from '@/features/clients/schemas/ClientSchemas';

export function CreateClientDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const [createClient, { isError }] = clientsApi.useCreateClientMutation();

  const form = useForm({
    defaultValues: { name: '', email: '', phoneNumber: '' },
    validators: { onSubmit: CreateClientRequestSchema },
    onSubmit: async ({ value }) => {
      const result = await createClient({
        name: value.name,
        email: value.email || null,
        phoneNumber: value.phoneNumber || null,
      });
      if ('data' in result) {
        onClose();
        navigate(`/clients/${result.data.id}`);
      }
    },
  });

  return (
    <Dialog open={open} onClose={onClose}>
      <DialogTitle>New Client</DialogTitle>
      <form onSubmit={(event) => { event.preventDefault(); void form.handleSubmit(); }}>
        <DialogContent>
          <Stack spacing={2} mt={1}>
            {isError && <Alert severity="error">We couldn’t create the client. Please try again.</Alert>}

            <form.Field name="name">
              {(field) => (
                <TextField
                  label="Name"
                  required
                  value={field.state.value}
                  onChange={(event) => field.handleChange(event.target.value)}
                  onBlur={field.handleBlur}
                  error={field.state.meta.errors.length > 0}
                />
              )}
            </form.Field>

            <form.Field name="email">
              {(field) => (
                <TextField label="Email" value={field.state.value} onChange={(event) => field.handleChange(event.target.value)} onBlur={field.handleBlur} />
              )}
            </form.Field>

            <form.Field name="phoneNumber">
              {(field) => (
                <TextField label="Phone" value={field.state.value} onChange={(event) => field.handleChange(event.target.value)} onBlur={field.handleBlur} />
              )}
            </form.Field>

            <form.Subscribe selector={(state) => state.errorMap.onSubmit}>
              {(formError) => (formError ? <Alert severity="warning">Enter an email or a phone number.</Alert> : null)}
            </form.Subscribe>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained">Create</Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
```

> TanStack Form v1 surfaces the Zod schema's object-level error (the `contact` refine, which maps to no single field) via the form-level error map. The selector `state.errorMap.onSubmit` reads that on-submit error; confirm the exact shape against the installed `@tanstack/react-form@1.x` and adjust the selector if needed (the requirement: when the schema fails, show "Enter an email or a phone number." and do not call the mutation). The `name` `min(1)` failure maps to the `name` field and blocks submit too.

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'pnpm test src/features/clients/dialogs/CreateClientDialog'` → PASS (3).

```bash
git add frontend/ && git commit -m "Add CreateClientDialog (TanStack Form + Zod, request capture, success navigation, 400 surfacing)"
```

---

## Task 12: Client detail page

**Files:** Create `src/features/clients/pages/ClientDetailPage.tsx`; Test `…/ClientDetailPage.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/features/clients/pages/ClientDetailPage.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { TestingApiServer } from '@/testing/TestingApiServer';
import { ModelFactory } from '@/testing/modelFactory';
import { ClientDetailPage } from '@/features/clients/pages/ClientDetailPage';

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

Run: `zsh -lc 'pnpm test src/features/clients/pages/ClientDetailPage'` → FAIL.

- [ ] **Step 3: Implement the page**

`frontend/src/features/clients/pages/ClientDetailPage.tsx`:

```tsx
import { useParams } from 'react-router-dom';
import { Alert, Box, CircularProgress, Stack, Typography } from '@mui/material';
import { clientsApi } from '@/features/clients/api/clientsApi';

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

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'pnpm test src/features/clients/pages/ClientDetailPage'` → PASS (2).

```bash
git add frontend/ && git commit -m "Add ClientDetailPage (details + not-found state)"
```

---

## Task 13: Auth guard, welcome, and unauthorized pages

**Files:** Create `src/routes/RequireAuth.tsx`, `WelcomePage.tsx`, `UnauthorizedPage.tsx`; Test `src/routes/RequireAuth.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/routes/RequireAuth.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { RequireAuth } from '@/routes/RequireAuth';

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
  it('renders protected content when authenticated', () => {
    renderWithProviders(<Protected />, { route: '/clients', auth: { isAuthenticated: true } });
    expect(screen.getByText('secret clients')).toBeInTheDocument();
  });

  it('triggers login (with returnTo) when unauthenticated', () => {
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

Run: `zsh -lc 'pnpm test src/routes/RequireAuth'` → FAIL.

- [ ] **Step 3: Implement the guard + pages**

`frontend/src/routes/RequireAuth.tsx`:

```tsx
import { useEffect } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { CircularProgress } from '@mui/material';
import { useAuth } from '@/shared/auth/auth';

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
import { Box, Link, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

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

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'pnpm test src/routes/RequireAuth'` → PASS (3).

```bash
git add frontend/ && git commit -m "Add RequireAuth guard (login returnTo, error→/unauthorized) and welcome/unauthorized pages"
```

---

## Task 14: Router and app shell

**Files:** Create `src/app/AppLayout.tsx`, `src/app/router.tsx`, `src/app/ConfiguredApp.tsx`, `src/app/App.tsx`; Replace `src/main.tsx`; Test `src/app/router.test.tsx`

- [ ] **Step 1: Write the failing test**

`frontend/src/app/router.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { Routes } from 'react-router-dom';
import { renderWithProviders } from '@/testing/renderWithProviders';
import { appRoutes } from '@/app/router';

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

Run: `zsh -lc 'pnpm test src/app/router'` → FAIL.

- [ ] **Step 3: Layout, routes, configured app, app, main**

`frontend/src/app/AppLayout.tsx`:

```tsx
import { AppBar, Box, Button, Container, Toolbar, Typography } from '@mui/material';
import { Outlet } from 'react-router-dom';
import { useAuth } from '@/shared/auth/auth';

export function AppLayout() {
  const auth = useAuth();
  return (
    <Box>
      <AppBar position="static">
        <Toolbar sx={{ justifyContent: 'space-between' }}>
          <Typography variant="h6">modern-fmis</Typography>
          {auth.isAuthenticated && <Button color="inherit" onClick={auth.logout}>{auth.userEmail ?? 'Log out'}</Button>}
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
import { AppLayout } from '@/app/AppLayout';
import { RequireAuth } from '@/routes/RequireAuth';
import { WelcomePage } from '@/routes/WelcomePage';
import { UnauthorizedPage } from '@/routes/UnauthorizedPage';
import { ClientsListPage } from '@/features/clients/pages/ClientsListPage';
import { ClientDetailPage } from '@/features/clients/pages/ClientDetailPage';

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

`frontend/src/app/ConfiguredApp.tsx`:

```tsx
import { useMemo } from 'react';
import { Auth0Provider, type AppState } from '@auth0/auth0-react';
import { Provider } from 'react-redux';
import { RouterProvider } from 'react-router-dom';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { CssBaseline } from '@mui/material';
import { useConfig } from '@/shared/config/ConfigContext';
import { AuthProvider } from '@/shared/auth/auth';
import { createStore } from '@/app/store';
import { router } from '@/app/router';

const theme = createTheme();

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
          <ThemeProvider theme={theme}>
            <CssBaseline />
            <RouterProvider router={router} />
          </ThemeProvider>
        </AuthProvider>
      </Auth0Provider>
    </Provider>
  );
}
```

`frontend/src/app/App.tsx`:

```tsx
import { ConfigProvider } from '@/shared/config/ConfigContext';
import { ConfiguredApp } from '@/app/ConfiguredApp';

export function App() {
  return (
    <ConfigProvider>
      <ConfiguredApp />
    </ConfigProvider>
  );
}
```

`frontend/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';

createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
```

- [ ] **Step 4: Run it (green), full suite, typecheck**

Run: `zsh -lc 'pnpm test && pnpm typecheck'` → all green.

- [ ] **Step 5: Commit**

```bash
git add frontend/ && git commit -m "Add app shell: layout, router, ConfiguredApp (store+Auth0+AuthProvider), thin main; deep-link returnTo"
```

---

## Task 15: Zod ↔ OpenAPI contract test

**Files:** Create `src/features/clients/schemas/clientContract.test.ts`, `src/features/clients/schemas/openapi.snapshot.json`

- [ ] **Step 1: Capture the backend OpenAPI snapshot**

With the backend running (`cd backend && zsh -lc 'dotnet run --project src/Fmis.Api'`, or `docker compose up backend`):

```bash
cd /Users/bryceklinker/code/uplift-delivery/modern-fmis
zsh -lc 'curl -s http://localhost:8080/openapi/v1.json' > frontend/src/features/clients/schemas/openapi.snapshot.json
```

Stop the backend afterward.

- [ ] **Step 2: Write the contract test**

`frontend/src/features/clients/schemas/clientContract.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import openapi from '@/features/clients/schemas/openapi.snapshot.json';
import { ClientResponseSchema, CreateClientRequestObjectSchema } from '@/features/clients/schemas/ClientSchemas';

function propsOf(schemaName: string): string[] {
  const schema = (openapi as { components?: { schemas?: Record<string, { properties?: Record<string, unknown> }> } })
    .components?.schemas?.[schemaName];
  return Object.keys(schema?.properties ?? {});
}

describe('client contract matches the backend OpenAPI document', () => {
  it('ClientResponseModel matches ClientResponseSchema', () => {
    expect(Object.keys(ClientResponseSchema.shape).sort()).toEqual(propsOf('ClientResponseModel').sort());
  });

  it('CreateClientRequestModel matches CreateClientRequestObjectSchema', () => {
    expect(Object.keys(CreateClientRequestObjectSchema.shape).sort()).toEqual(propsOf('CreateClientRequestModel').sort());
  });
});
```

- [ ] **Step 3: Run it**

Run: `zsh -lc 'pnpm test src/features/clients/schemas/clientContract'` → PASS (schema property names equal the OpenAPI schema property names). A failure means the schemas drifted from the backend — fix the Zod schema (backend is the source of truth). (Requires `resolveJsonModule` — Vite's default tsconfig has it.)

- [ ] **Step 4: Commit**

```bash
git add frontend/ && git commit -m "Add Zod↔OpenAPI contract test against the backend schema snapshot"
```

---

## Task 16: Dockerfile and docker-compose frontend service

**Files:** Create `frontend/Dockerfile`, `frontend/nginx.conf`, `frontend/.dockerignore`; Modify `docker-compose.yml`

- [ ] **Step 1: Dockerfile (build once, serve static)**

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

- [ ] **Step 2: nginx SPA config**

`frontend/nginx.conf`:

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;
  location / { try_files $uri $uri/ /index.html; }
}
```

- [ ] **Step 3: `.dockerignore`**

`frontend/.dockerignore`:

```
node_modules
dist
```

- [ ] **Step 4: Add the frontend service to `docker-compose.yml`**

Under `services:` in the existing `docker-compose.yml`:

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

> The committed `public/config.json` (apiBaseUrl `http://localhost:8080`) is baked into the static assets for local dev; deployed environments get their `config.json` from the `application` Pulumi stack. Full login won't complete locally until a real Auth0 tenant exists (infra phase).

- [ ] **Step 5: Build the image to verify it bundles**

```bash
cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/frontend
zsh -lc 'docker build -t fmis-frontend-test .'
```
Expected: build succeeds through the nginx stage. Then `zsh -lc 'docker rmi fmis-frontend-test'`.

- [ ] **Step 6: Commit**

```bash
git add frontend/Dockerfile frontend/nginx.conf frontend/.dockerignore docker-compose.yml
git commit -m "Add frontend Dockerfile (build once, nginx-served) and docker-compose service"
```

---

## Done criteria

- `cd frontend && zsh -lc 'pnpm test'` passes all tests; `pnpm typecheck` clean; `pnpm build` produces `dist/`.
- The Client UI works against the backend contract: list (loading/error), create dialog (TanStack Form + Zod incl. email-or-phone, request carries the data, success → detail, 400 surfaced), detail (with not-found).
- **No global mutable holders**: API base URL and Auth0 token flow through the store (`createStore(config)` + `AuthProvider` dispatching the token); `baseQuery` reads both from state.
- Self-loading `ConfigProvider` (Zod-validated, optional `config` prop for tests); thin `main.tsx`; provider order `Provider → Auth0Provider → AuthProvider → Router`.
- Routing/auth: `/`→`/welcome`, guarded routes, `/unauthorized`, deep-link `returnTo` preserved.
- Conventions honored: feature subfolders, `@/` aliases (testing via `@/testing`), PascalCase schemas, no parse wrappers, `UPPER_SNAKE_CASE` constants (`TEST_CONFIG`, `DEFAULT_AUTHENTICATED_STATE`, `API_TAGS`), `safeParse` failure tests, `RequestCapture` class, centralized `src/testing/` harness, behavior-first tests (role/label queries, MSW edge, no snapshots/self-mocking).
- **Deferred:** Playwright E2E + live integrated login (await the Auth0/infrastructure phase).
```
