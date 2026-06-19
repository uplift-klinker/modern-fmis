# modern-fmis Infrastructure Foundations + Auth0 (Phase 3a) — Design Spec

**Date:** 2026-06-18
**Status:** Approved for planning
**Scope:** Phase 3a — the first slice of the Infrastructure/Auth0 phase. Stands up the Pulumi (C#) foundation and the **`auth` stack** (Auth0) for a single `dev` environment, driven from CI, emitting the real Auth0 values the app needs. Phases 1 (backend) and 2 (frontend) are complete on `main`.

---

## Purpose

Establish the Pulumi-on-Azure foundation and provision Auth0 (authentication only) so the rest of the infrastructure phase has: a working self-managed Pulumi state backend, a CI-driven deploy flow, a consistent resource-naming convention, and **real Auth0 values** (`domain`, SPA `clientId`, `audience`) that the backend (JWT validation) and frontend (`config.json`) consume. It also provisions, conditionally, the Auth0 test user + client the future Playwright suite needs to obtain a token without manual setup.

This is the first of three infrastructure sub-phases:

- **3a (this spec)** — Pulumi foundations + Auth0 `auth` stack.
- **3b** — `persistence` (PostgreSQL/PostGIS, deletion-protected) + `application` (compute, static hosting, `config.json` emission) stacks, extending the CI pipeline to deploy them.
- **3c** — live integrated login + the Playwright E2E suite.

### Success criteria

- `infra/` builds; the `Fmis.Infra.Tests` unit suite is green.
- The tenant-setup runbook is followable to bootstrap a tenant from scratch.
- On merge to `main`, CI bootstraps the state backend (idempotent) and `pulumi up` provisions the Auth0 **SPA application**, the **API/resource server** (env-named audience), the tenant `default_directory`, and — in `dev` — the **e2e test user + client**.
- The `auth.dev` stack exposes the outputs 3b/3c consume: `domain`, `spaClientId`, `audience`, and (when enabled) the e2e creds as secret outputs.

### Explicit non-goals (deferred)

- **`persistence` and `application` stacks**, `config.json` emission, and deployed-URL callback/origin wiring on the SPA app — Phase 3b.
- **The Playwright E2E suite itself** and live integrated login — Phase 3c. 3a only *provisions* the Auth0 resources/creds those tests will use.
- **Authorization** (roles/permissions). Auth0 is authentication only.
- **Multiple environments.** A single `dev` environment, with `infra/` structured so a second environment is additive (a new stack + a new tenant).
- **Azure Key Vault.** Pulumi secret encryption uses a passphrase; no Key Vault in this phase.

---

## Environment & naming

- **One environment: `dev`.** Each Pulumi project has a `dev` stack (`auth.dev`). Environment is derived from the stack name.
- **Consistent naming for every Pulumi resource (Auth0 + Azure)** via a single helper in `Fmis.Infra.Common`, based on environment + infra layer (`auth`/`persistence`/`app`/…):
  - Dash-friendly resources (most Azure types, Auth0 resource *names*): `fmis-<env>-<layer>-<resource>` — e.g. `fmis-dev-auth-spa`, `fmis-dev-auth-api`.
  - Constrained resources (e.g. Azure storage accounts: lowercase-alphanumeric, ≤24 chars, globally unique): compacted `fmis<env><layer><purpose>` + a short random suffix.
  - Auth0 API identifier/audience (env-first, URL-style, need not resolve): `https://<env>.api.modern-fmis` → **`https://dev.api.modern-fmis`**.

---

## Manual bootstrap (documented runbook)

Auth0 tenants cannot be created by IaC, and the GitHub→Azure CI identity cannot create itself, so a one-time manual bootstrap is unavoidable. A new **`docs/auth0-tenant-setup.md`** documents it — covering both the initial `dev` tenant and the steps to stand up an additional tenant later (named so it's obviously the "set up a new tenant" guide):

1. **Auth0 tenant + management M2M app.** Create the tenant; create a Machine-to-Machine application authorized for the Auth0 Management API; capture its `domain`, `clientId`, `clientSecret`.
2. **GitHub→Azure OIDC identity.** Via `az` CLI (one time): an Azure AD app registration + a federated credential scoped to this GitHub repo + a Contributor role assignment on the target subscription — the identity CI assumes.
3. **Place secrets.** Store the Auth0 M2M creds as **GitHub environment secrets**; store the **Pulumi passphrase** as a GitHub secret.

Everything else (resource group, state storage account, container, and all Pulumi stacks) is CI-driven and idempotent.

---

## State backend bootstrap

`infra/scripts/bootstrap-state.sh` — an **idempotent** `az` CLI script (create-if-not-exists) that provisions the resource group, the `azblob` state **storage account**, and the state container. It runs in CI (authenticated via Azure OIDC) **before any Pulumi command**, after which CI runs `pulumi login azblob://<container>?storage_account=<account>`. The state-backend storage account is **not** a Pulumi-managed resource (it holds Pulumi's own state).

---

## `infra/` layout

```
infra/
  Fmis.Infra.slnx
  Fmis.Infra.Common/          # naming helper (fmis-<env>-<layer>-<resource>), env-from-stack, shared config
  auth/                       # the auth Pulumi project
    Pulumi.yaml               # name: fmis-auth, runtime: dotnet
    Pulumi.dev.yaml           # dev stack config (non-secret)
    Program.cs                # composes the auth stack from Common
    Fmis.Infra.Auth.csproj    # references Fmis.Infra.Common
  scripts/
    bootstrap-state.sh        # idempotent az CLI: resource group + azblob state storage account + container
  tests/
    Fmis.Infra.Tests/         # Pulumi.Testing unit tests (xUnit)
  (later) persistence/, application/   # added in 3b, same shape
```

Each layer is its own Pulumi project (`fmis-auth`, later `fmis-persistence`, `fmis-application`), each with a `dev` stack, sharing `Fmis.Infra.Common` so the naming convention lives in one place. The future `application` stack reads the `auth` stack's outputs via a Pulumi **stack reference**.

---

## The `auth` stack

Provisions, inside the manually-bootstrapped tenant (Pulumi authenticates with the M2M management creds via the Auth0 provider):

- **SPA Application** (`fmis-dev-auth-spa`) — `token_endpoint_auth_method` none (public SPA); allowed callback / logout / web origins = `http://localhost:5173` for local dev against the real tenant. (Deployed URLs are added in 3b.)
- **API / resource server** (`fmis-dev-auth-api`) — identifier `https://dev.api.modern-fmis`.
- **Tenant `default_directory`** → the tenant's database connection (required so ROPG resolves a user). This is the only tenant-level setting Pulumi manages; the rest is untouched and can be extended later.
- **Conditional E2E provisioning**, gated by Pulumi config flag `enableE2eUser` (true in `dev`):
  - a **test user** in the database connection, with a Pulumi-generated `RandomPassword`;
  - a dedicated **e2e application** (`fmis-dev-auth-e2e`) with the **password grant** enabled, scoped to the API audience.

### Outputs

- Always: `domain`, `spaClientId`, `audience`.
- When `enableE2eUser`: `e2eClientId`, `e2eClientSecret`, `e2eUsername`, `e2ePassword` — as **secret** stack outputs.

These outputs are how the backend (audience/domain for JWT validation), the frontend (`config.json` in 3b), and the Playwright suite (ROPG token in 3c) obtain real values.

---

## Secrets

- **Pulumi state-secret encryption:** a **passphrase** (`PULUMI_CONFIG_PASSPHRASE`), stored as a GitHub secret and available to local runs.
- **Auth0 M2M management creds:** GitHub **environment** secrets, injected into the Pulumi run as `auth0:domain` / `auth0:clientId` / `auth0:clientSecret` config (the Auth0 provider config).
- **E2E creds:** Pulumi-generated (`RandomPassword`), never manual; surfaced as secret stack outputs for the 3c e2e job.

---

## CI/CD (GitHub Actions — this phase runs the `auth` stack only)

- **Pull request → `main`:** lint/build/test the infra (`dotnet` build + `Fmis.Infra.Tests`) → run `bootstrap-state.sh` (idempotent) → `pulumi login` → **`pulumi preview`** on `auth.dev`.
- **Merge → `main`:** `bootstrap-state.sh` → `pulumi login` → **`pulumi up`** on `auth.dev`.
- Azure authentication via **GitHub OIDC** (the bootstrapped federated identity). The Pulumi passphrase and Auth0 M2M creds come from GitHub secrets.
- 3b extends this same workflow to the `persistence` and `application` stacks.

---

## Testing (TDD)

Pulumi C# is production code, so it is test-driven with the **`Pulumi.Testing`** mock framework (`Deployment.TestAsync`, xUnit — matching the backend), in `infra/tests/Fmis.Infra.Tests`. The suite asserts:

- the naming helper yields `fmis-<env>-<layer>-<resource>` (and the compacted form for constrained resources);
- the API resource server identifier is the env-named `https://dev.api.modern-fmis`;
- the SPA application and API/resource server are created with the expected names;
- the tenant `default_directory` is set;
- the **conditional** e2e test user + e2e application appear **only when** `enableE2eUser` is set, and are **absent when it is off**;
- the expected stack outputs are populated (and the e2e outputs are marked secret).

No real cloud calls; `pulumi preview` in CI catches wiring beyond what mocks cover.

---

## What later phases inherit

3b adds the `persistence` and `application` stacks (same project shape + naming helper), wires the SPA app's deployed callbacks, emits `config.json` from the auth/persistence outputs, and extends the CI workflow to deploy them. 3c consumes the e2e outputs to drive a real-token Playwright suite and validates live integrated login.
