# Phase 3c — Live Integrated Login + Playwright E2E — Design

**Date:** 2026-07-03
**Status:** Approved (brainstorming complete; ready for implementation planning)
**Depends on:** Phase 3a (auth stack), Phase 3b (persistence + identity + application stacks deploying to Azure).

## Goal

Verify modern-fmis runs on Azure end-to-end with a **real Auth0 login**, and add a **Playwright E2E suite** that exercises the deployed dev environment as one system. This closes the roadmap item: *"a Playwright smoke E2E (incl. Auth0 login)."*

## Architecture at a glance

- The **SPA Auth0 client moves out of the auth stack into the application stack**, because it is specific to the deployed frontend and needs the deployed frontend URL as an allowed callback. The auth stack keeps the shared platform: tenant config, the API/resource server, and the e2e access.
- A new **top-level `e2e/` pnpm workspace** holds the Playwright system tests, shared utilities, and the relocated contract tests. The repo becomes a pnpm workspace (`frontend` + `e2e`).
- **CD runs gate → deploy → verify**: a single approval gate, one deployment, and automatic post-deploy verification (full e2e on dev; smoke on other environments).

## Decisions (the forks we resolved)

1. **Login strategy = hybrid.** One real interactive-login test (belongs to both the e2e and smoke suites), plus token-short-circuit tests using a generated token for everything else.
2. **Trigger = automatic post-CD, dev only.** Dev runs the full e2e suite on every deploy. Future environments run only the smoke test (verify basic login is unbroken).
3. **SPA client ownership = application stack.** The application stack creates and configures its own SPA client in the tenant, avoiding value round-trips, double deploys, and custom resources.
4. **E2E is a system concern at the repo top level**, with pnpm workspaces so dotnet and pnpm tooling avoid awkward cross-directory pathing.
5. **Contract tests move into `e2e/`** and are rewritten onto Playwright's runner so they share e2e utilities and import the frontend's Zod schemas via a workspace dependency.
6. **CD gate-job pattern.** Only the gate job carries `environment: {env}` (single approval); it exposes env-specific outputs consumed by deploy + verify.

## Part 1 — Auth re-slice: move the SPA client to the application stack

### Auth stack (`infra/auth`)
- **Remove** `Components/SpaApplication.cs` and its use in `AuthStack.cs`; **drop the `spaClientId` output.**
- **Keep** `TenantConfiguration`, `AuthApi` (emits `domain` / `audience`), and `E2eTestAccess` (emits `e2eClientId` / `e2eClientSecret` / `e2eUsername` / `e2ePassword`).
- **Extend `TenantConfiguration`** to relax Auth0 attack protection **for the dev tenant** (suspicious-IP throttling / brute-force / bot detection) so automated interactive logins from CI runners are not blocked.
- **Tests:** auth-stack tests drop the SPA-client assertions and add assertions for the relaxed dev attack-protection settings.

### Application stack (`infra/application`)
- Add a `Pulumi.Auth0` **3.45.0** package reference to `Fmis.Infra.Application.csproj`.
- Add a new component `Components/SpaClient.cs` that creates:
  - `Auth0.Client` — `AppType = "spa"`, `OidcConformant = true`, grant types `authorization_code` + `refresh_token`, and `Callbacks` / `AllowedLogoutUrls` / `WebOrigins` = **`http://localhost:5173`** *and* the deployed **`frontendUrl`**.
  - `Auth0.ClientCredentials` — `AuthenticationMethod = "none"`.
  - Exposes `ClientId`.
- `ApplicationStack` wires `SpaClient` and passes its `ClientId` into `FrontendSite.WriteConfig` (replacing today's `auth.RequireString("spaClientId")`).
- The Auth0 provider authenticates against the same tenant via the `AUTH0_DOMAIN` / `AUTH0_CLIENT_ID` / `AUTH0_CLIENT_SECRET` env the pipeline already injects into every stack step — no new wiring.
- **`config.json` schema is unchanged**: `apiBaseUrl`, `auth.domain`, `auth.clientId` (now the locally-created id), `auth.audience`.
- **Tests:** assert the `Auth0.Client` (`spa`) exists with callbacks containing the deployed frontend origin *and* localhost; assert `ClientCredentials` (`none`); assert `config.json` carries the local client id. `StackMocks` gains an Auth0 client-id mock and drops `spaClientId` from the auth-reference outputs.

## Part 2 — The `e2e/` workspace

### Repo becomes a pnpm workspace
- Add root `pnpm-workspace.yaml` with `packages: [frontend, e2e]`.
- Add a minimal private root `package.json`; move the `packageManager: pnpm@11.7.0` Corepack pin here from `frontend/package.json`.
- `pnpm install` at the repo root installs both workspaces.

### `e2e/` layout
- `e2e/support/` — shared utilities:
  - config/URL discovery from env vars;
  - `generateToken()` — POST Auth0 `/oauth/token` password-realm grant (e2e client + user, `scope: openid profile email`, API `audience`) → access + id token;
  - `interactiveLogin(page)` — navigate to the app → follow redirect to Auth0 Universal Login → fill the e2e user's email/password → land on `/welcome`;
  - `seedAuthSession(page, token)` — pre-populate the `@auth0/auth0-spa-js` localStorage cache from a generated token so the app boots already authenticated;
  - backend request helpers (Bearer token).
- `e2e/system/` — Playwright tests run against **deployed dev**:
  1. `login.smoke.spec.ts` **`@smoke`** — real interactive login → assert the authenticated AppBar (logout button showing the e2e email) on `/welcome`. This is the single test in both suites.
  2. `authenticated-app.spec.ts` — token short-circuit → app renders authenticated without a redirect → assert protected UI.
  3. `api.spec.ts` — token short-circuit at the API level: send the generated token as a Bearer to `GET {backendUrl}/clients` → expect **200** (proves a real Auth0 JWT is accepted by the real backend end-to-end).
- `e2e/contract/` — the relocated contract test, **rewritten onto Playwright's runner** (API tests via the `request` fixture). Playwright's `webServer` spins up the backend via `docker compose`; it validates the frontend's hand-written Zod schemas against the live `/openapi/v1.json`. Runs at PR time, no deploy needed, no browser binaries.
- `e2e` declares `"frontend": "workspace:*"` so `contract/` imports the Zod schemas directly (`ClientResponseSchema`, `ClientListSchema`, `CreateClientRequestObjectSchema`) with no `../../frontend/src` pathing.
- `e2e/playwright.config.ts` — projects for `system` (deployed dev) and `contract` (local backend); CI hardening: `retries: 2`, `trace: on-first-retry`, sane timeouts; Chromium.
- `e2e` scripts: `test:contract` (contract project, local backend), `test:e2e` (full system project, deployed dev), `test:smoke` (`--grep @smoke`).

### Frontend workspace changes
- `frontend/` keeps its vitest **unit** tests.
- **Remove** from `frontend/`: `test:contract` script, `vitest.contract.config.ts`, `contract.setup.ts`, and `src/features/clients/schemas/client-contract.contract.ts` (moves to `e2e/contract/`).

### Solution access
- Add an `e2e` solution folder to `Fmis.slnx` surfacing `playwright.config.ts` + `package.json` for IDE navigability (folder/file entries — `e2e` is a TypeScript project, not a buildable .NET project).

## Part 3 — CI/CD

### Main CI (PR + push to main) — adjusted for the workspace root
- **Frontend job:** root `pnpm install --frozen-lockfile`, then `pnpm --filter frontend lint | typecheck | test | build`.
- **Contract job:** root install, then `pnpm --filter e2e test:contract` (Playwright `webServer` runs the backend via `docker compose`; validates Zod vs `/openapi/v1.json`). No deploy, no browser binaries.
- **Backend job** and **Preview job:** unchanged.

### Main CD — gate → deploy → verify (single approval, single deployment)
- **`gate` job** — `environment: {env}`. The *only* job referencing the environment, so the single required-reviewer approval per run. Emits outputs: the environment name and the verify suite selector (`e2e` for dev, `smoke` for other envs). No secrets pass through outputs (job outputs are not masked); secrets stay repo-level and are read directly by the jobs that need them.
- **`deploy` job** — `needs: gate`, **no** `environment`. Runs the Pulumi stacks up (auth → persistence → identity → application), parameterized by the gate's outputs.
- **`verify` job** — `needs: deploy`, **no** `environment`. Reads the deployed `frontendUrl` / `backendUrl` + e2e creds from Pulumi outputs (secret outputs masked), then runs `pnpm --filter e2e test:${{ needs.gate.outputs.suite }}` — dev → full e2e, other envs → smoke.

### Prerequisite (one-time, operator action)
- Because `deploy` and `verify` no longer carry `environment: dev`, their GitHub OIDC token subject is no longer `…:environment:dev`. **Add an Azure federated credential for the branch subject** `repo:uplift-klinker/modern-fmis:ref:refs/heads/main` so those jobs can `azure/login`.
- **Security note:** a branch-scoped credential means the gate enforces *approval + ordering*, not a hard Azure-credential boundary — acceptable for a protected `main`, stated explicitly.

### Reusable workflow interaction
- `pulumi-stacks.yml` currently hard-codes `environment: {env}` and is shared with CI preview. Make that environment binding **conditional**: CD drives approval via the gate job and passes no environment; CI preview keeps its current behavior so it is not disrupted.

## Part 4 — Testing & verification approach

- **Infra changes are TDD'd via Pulumi.Testing** (auth-stack SPA removal + dev attack-protection; application-stack SPA client + callbacks + config id).
- **Contract suite** runs green at PR time against a locally composed backend.
- **E2E suite** runs post-deploy against dev; the e2e workspace is linted/typechecked in CI.
- **Playwright hardening:** `retries: 2`, `trace: on-first-retry`, pinned auth0 lib versions (the `seedAuthSession` helper depends on the `@auth0/auth0-spa-js` localStorage cache format and is kept in one place).
- **No backend code change:** the token→API test uses the existing `[Authorize]` `GET /clients`; CORS already allows the SPA origin. YAGNI — no new `/health` endpoint.
- **Honest caveat:** the live interactive-login path can only be fully confirmed once it runs against deployed dev on the first CD after this lands. The plan calls that out as the final verification step rather than pretending unit tests cover it.

## Conventions honored

- No code comments (self-documenting names; rationale in docs).
- Exact pinned versions (`Pulumi.Auth0` 3.45.0; existing pins unchanged).
- TDD for all infra (Pulumi.Testing) and the contract suite.
- Append-only git (new commits only, never amend/force-push).
- ComponentResource composition + thin stacks; `ResourceNames.For(env, layer, resource)`.
- Default region centralus (no new Azure resources introduced here).

## Out of scope

- A branded custom domain for the frontend (would remove the localhost/deployed-URL callback duality; deferred to a later phase).
- Additional environments beyond dev (the design is multi-env-ready: gate outputs select `e2e` vs `smoke`).
- Backend feature endpoints beyond the existing `clients` slice.
