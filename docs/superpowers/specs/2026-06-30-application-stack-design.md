# modern-fmis Application Stack (Phase 3b‑2) — Design Spec

**Date:** 2026-06-30
**Status:** Approved for planning
**Scope:** Phase 3b‑2 — the second slice of Phase 3b (Azure runtime). Runs the backend and frontend on Azure, consuming the `auth` and `persistence` stack outputs. Phase 3a (`auth`) and Phase 3b‑1 (`persistence`) are merged on `main`.

---

## Purpose

Stand up the **application tier** so modern-fmis actually runs on Azure end to end: the backend API in **Azure Container Apps** (connecting to the Phase 3b‑1 Postgres via the `fmis-dev-app-identity` managed identity — no password), the frontend as a **static site in Azure Blob Storage** with a **Pulumi-generated `config.json`**, and a CD job that builds, ships, and deploys both. The application tier is freely redeployable (no deletion protection, per [`infrastructure-tiers.md`](../../conventions/infrastructure-tiers.md)).

### Success criteria

- `infra/` builds; `Fmis.Infra.Tests` (now including application tests) is green; backend `dotnet test` + the Zod↔OpenAPI contract test stay green; `docker compose` still runs the stack locally unchanged.
- On merge, CD deploys the `application` stack: ACR + the backend Container App (managed identity, `AcrPull`, external ingress, scale-to-zero) + the frontend static website + `config.json`, all via `pulumi up` (Pulumi builds and pushes the backend image).
- The deployed backend reaches Postgres using an **Entra token** (the managed identity), no password anywhere; the deployed SPA loads its runtime `config.json` and calls the API cross-origin.

### Explicit non-goals (deferred)

- **Phase 3c** — live integrated login (real Auth0 round-trip) and Playwright E2E.
- **`CREATE EXTENSION postgis`** — the Field phase, when the first spatial tables appear.
- **Custom domains, CDN/Front Door, WAF, private networking** — later hardening; 3b‑2 uses the default Container Apps ingress FQDN and the Storage static-website endpoint.
- **Multiple environments** — a single `dev` environment, structured so another is additive.

### Rejected alternative (recorded)

**Azure Functions for the backend was considered and rejected.** Hosting the existing ASP.NET Core MVC API on Functions is not first-class: the ASP.NET Core Integration does not host `[ApiController]` controllers (the catch-all-proxy workaround fails), so it would force rewriting the HTTP edge as `[HttpTrigger]` functions **and** replacing the `AddOpenApi()` document that the Zod↔OpenAPI contract test depends on. **Azure Container Apps** runs the existing ASP.NET Core container unchanged — controllers, OpenAPI, the contract test, and all integration tests keep working — and still **scales to zero** (`minReplicas: 0`), giving the cheap-when-idle benefit without the rewrite. (Refs: azure-functions-dotnet-worker #2744.)

---

## Environment & naming

- Single `dev` environment; a new Pulumi project `fmis-application` with a `dev` stack, sharing `Fmis.Infra.Common` (`ResourceNames`, env-from-stack) and reading the `auth` and `persistence` stacks via stack references.
- Naming via `ResourceNames.For(env, "application", …)` (e.g. `fmis-dev-application-backend`, `fmis-dev-application-env`). The ACR and Storage account need globally-unique, alphanumeric-constrained names (compacted form, e.g. `fmis${env}acr` / `fmis${env}web`).
- Default region `centralus` (overridable via `AZURE_LOCATION`).

---

## Backend code changes (local behavior unchanged; TDD)

The backend stays a full ASP.NET Core app. Two small, config-driven additions, the first slice of the plan:

1. **Entra-token Postgres auth.** A configuration flag `Database:UseEntraAuth` (set `true` on the Container App; unset/`false` locally) selects how `Fmis.Core` builds its Npgsql data source:
   - **Azure (`true`):** the connection string carries no password (`Host=<serverFqdn>;Database=fmis;Username=fmis-dev-app-identity;Ssl Mode=Require`); the data source is configured with a token provider that fetches an Entra access token via `Azure.Identity` `DefaultAzureCredential` (scope `https://ossrdbms-aad.database.windows.net/.default`). `AZURE_CLIENT_ID` (= the persistence `appIdentityClientId`) selects the user-assigned identity.
   - **Local (`false`/unset):** the existing username/password connection string is used as-is — `docker compose` is untouched.
2. **CORS.** The SPA is served from the Storage static-website origin and calls the Container App API cross-origin, so the API allows the frontend origin from configuration (`Cors:AllowedOrigin`, injected by Pulumi) via `AddCors`/`UseCors`.

Both are covered by tests (the data-source-mode selection and the CORS policy) and leave the local stack, the existing api/core tests, and the contract test unchanged.

---

## Application stack components (Pulumi `ComponentResource`s, thin root)

`ApplicationStack` is a thin composition root over:

### `ContainerRegistry`
- Azure Container Registry, **Basic** SKU (cheapest), admin user disabled (pull is via managed identity).
- Exposes: `LoginServer`, the registry resource (for the image push + `AcrPull` grant).

### `BackendApp`
- A **Container Apps Environment** + a **Container App** running the backend image (built/pushed by Pulumi — see the pipeline).
- Assigned the **user-assigned managed identity `fmis-dev-app-identity`** (from persistence), granted **`AcrPull`** on the ACR (so the app pulls without registry creds), and the identity is the registry credential for the app.
- **External ingress** targeting port **8080** (the Dockerfile's `ASPNETCORE_HTTP_PORTS`); **scale-to-zero** (`minReplicas: 0`, a small `maxReplicas`).
- Environment variables: the token-mode `ConnectionStrings__Fmis`, `Database__UseEntraAuth=true`, `AZURE_CLIENT_ID` (= `appIdentityClientId`), `Auth0__Authority` (= `https://{auth.domain}/`), `Auth0__Audience` (= `auth.audience`), and `Cors__AllowedOrigin` (= the frontend static-website URL).
- Exposes: `Url` (the ingress FQDN).

### `FrontendSite`
- A **Storage account** (Standard_LRS) with **static website hosting** enabled (`$web` container, `index.html` document + SPA fallback).
- The built **`dist/`** uploaded to `$web` (Pulumi synced-folder).
- A Pulumi-authored **`config.json`** blob in `$web` with content `{ apiBaseUrl: <BackendApp.Url>, auth: { domain, clientId, audience } }` (clientId = the auth stack's `spaClientId`).
- Exposes: `Url` (the static-website primary endpoint).

The two URLs are independently derivable (the storage static-website endpoint from the account; the ingress FQDN from the app), so wiring the backend's CORS origin to the frontend URL and the frontend's `apiBaseUrl` to the backend URL has no cyclic dependency.

---

## Image & asset pipeline (Pulumi-driven)

Within `pulumi up`, Pulumi orders the whole graph:

1. `ContainerRegistry` (ACR) is created.
2. A Pulumi **`docker-build`** image resource builds `backend/src/Fmis.Api/Dockerfile` with the **repo-root context** (the post-consolidation context) and pushes it to the ACR.
3. `BackendApp`'s Container App references that pushed image.
4. `FrontendSite` uploads `dist/` and writes `config.json`.

Building the image during `pulumi up` requires Docker on the runner (the GitHub runner has it). The frontend assets are produced by `pnpm build` (in CD) before `pulumi up`, and Pulumi uploads the resulting `dist/`.

---

## CD

Replace the placeholder `apps` job in `cd.yml` with the real deploy. It is a **dedicated job** (not routed through the infra-tier `infra-deploy.yml`, since it additionally builds the frontend assets and the backend image), `needs: [infra]` so the auth/persistence stacks exist first, and carries `environment: dev` (for the environment-scoped Azure OIDC) plus the explicit repo-level secrets. Its steps: checkout → setup-dotnet + setup-node (Corepack) + `azure/login` + `pulumi/actions` → `pulumi login` to the shared azblob backend → `pnpm build` (produce `frontend/dist/`) → `pulumi up` on `infra/application` (which builds + pushes the backend image, uploads `dist/`, and writes `config.json`). The application stack reads the `auth`/`persistence` outputs via stack references over that shared backend; the state backend is already bootstrapped by the `infra` job.

---

## Outputs

`ApplicationStack` emits `backendUrl` (the Container App ingress FQDN) and `frontendUrl` (the static-website endpoint) — the entry points Phase 3c (Playwright/login) will target.

---

## Testing (TDD)

- **Backend:** xUnit tests for the data-source-mode selection (Entra-token vs password by `Database:UseEntraAuth`) and the CORS policy; existing api/core tests + the Zod↔OpenAPI contract test stay green; `docker compose` unchanged.
- **Infra:** `Pulumi.Testing` asserts the application resource graph — the ACR, the Container Apps environment + app (the assigned identity, `AcrPull` role assignment, external ingress on 8080, `minReplicas: 0`, the env vars sourced from the auth/persistence stack references), the Storage account with static website + the uploaded assets + the `config.json` blob content, and the stack outputs. The actual image build/push and asset upload are data-plane effects verified by the deploy, not unit tests (mocks register the resources without executing them); stack references are mocked.

---

## What 3c inherits

`backendUrl` + `frontendUrl` are the live endpoints for Phase 3c — the real Auth0 login round-trip and the Playwright E2E.
