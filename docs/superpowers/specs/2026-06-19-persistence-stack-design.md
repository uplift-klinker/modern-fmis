# modern-fmis Persistence Stack (Phase 3b‑1) — Design Spec

**Date:** 2026-06-19
**Status:** Approved for planning
**Scope:** Phase 3b‑1 — the first slice of Phase 3b (Azure runtime). Provisions the PostgreSQL persistence tier and the managed identity authorized to use it, deletion-protected, deployed from CI. Phase 3a (auth stack + infra foundation) is merged on `main`.

---

## Purpose

Stand up the durable data tier modern-fmis runs on: an Azure Database for PostgreSQL Flexible Server with Entra-only authentication, a user-assigned managed identity authorized (entirely through Pulumi) to access the database, and the deletion-protection the persistence tier requires. It emits the values Phase 3b‑2 (the `application` stack) needs to run the backend against this database **without any password**.

This is the first of two Phase 3b sub-phases:

- **3b‑1 (this spec)** — `persistence` stack: Postgres Flexible Server + PostGIS-capable + the app's DB managed identity + authorization.
- **3b‑2** — `application` stack: backend compute (assigned the managed identity, connecting via an Entra token) + frontend hosting + `config.json`, consuming the `auth` and `persistence` stack outputs; the backend code change to support Entra-token auth in Azure (password auth retained for local docker-compose).

### Success criteria

- `infra/` builds; the `Fmis.Infra.Tests` suite (now including persistence tests) is green.
- On merge, CD deploys the `persistence` stack: a Burstable Postgres Flexible Server (Entra-only auth), the `fmis` database, the PostGIS allowlist, the user-assigned managed identity, its Entra-mapped Postgres principal, and its database privileges — all via `pulumi up`.
- The server is deletion-protected (Pulumi `protect` + Azure `CanNotDelete` lock).
- The stack exposes the outputs 3b‑2 consumes: server FQDN, database name, and the managed identity's `clientId` / `principalId` / name. **No password is produced anywhere.**

### Explicit non-goals (deferred)

- **The `application` stack** (backend/frontend compute, `config.json`), the backend's Entra-token auth code change, and **assigning the managed identity to the compute** — Phase 3b‑2.
- **`CREATE EXTENSION postgis`** — 3b‑1 only adds PostGIS to the server's extension allowlist; the extension is created in the Field phase when the first spatial tables appear (no spatial usage yet).
- **Private networking (VNet / private endpoint)** and **Entra-group DB admins** — later hardening when real environments arrive. 3b‑1 uses public access + firewall and the CI principal as the lone Entra admin.
- **Multiple environments** — a single `dev` environment, structured so another is additive.

---

## Environment & naming

- Single `dev` environment; a new Pulumi project `fmis-persistence` with a `dev` stack, sharing `Fmis.Infra.Common` (`ResourceNames`, env-from-stack) with the other stacks.
- Naming via `ResourceNames.For(env, "persistence", …)`: e.g. `fmis-dev-persistence-postgres` (server). The **database** is named **`fmis`** (not prefixed) to match what the backend connects to (`Database=fmis`). The managed identity is `fmis-dev-app-identity` (it represents the application's identity, created here so persistence owns DB authorization; 3b‑2 assigns it to the compute). Azure storage-account-style constrained names are not needed in this stack.
- Default region `centralus` (per the standing convention; `AZURE_LOCATION` overrides).

---

## Components (Pulumi `ComponentResource`s, thin stack root)

Per the stack-composition convention, `PersistenceStack` is a thin composition root over two components.

### `PostgresServer`
- **Azure Database for PostgreSQL Flexible Server**: SKU `B_Standard_B1ms` (Burstable — cheapest), version **16** (matches the local `postgis/postgis:16-3.4` image), 32 GB storage with auto-grow, 7-day backups.
- **Entra-only authentication**: `authConfig` with `activeDirectoryAuth = Enabled`, `passwordAuth = Disabled`. No administrator login/password.
- **Entra administrator**: an `Administrator` resource setting the **CI deploy service principal** as the server's Entra admin (its object id + tenant + display name + `ServicePrincipal` type). The deploy principal's object id is supplied to the stack via environment (e.g. `DEPLOY_PRINCIPAL_OBJECT_ID`) — see CD.
- **Public access + firewall**: an `AllowAllAzureServices` firewall rule (`0.0.0.0`) so the runtime compute reaches the server, plus a **deployer-IP firewall rule** built from the `DEPLOYER_IP` env var (the runner's/dev's egress IP) so `pulumi up` can run the data-plane authorization. The deployer rule is recreatable and may change per run.
- **PostGIS allowlist**: the server's `azure.extensions` parameter includes `POSTGIS` (the extension is *allowed*, not yet created).
- **Database**: a `fmis` database on the server.
- Exposes: `Fqdn`, `DatabaseName`.

### `DatabaseIdentity`
- **User-assigned managed identity** `fmis-dev-app-identity` (`UserAssignedIdentity`).
- **Entra-mapped Postgres principal**: a `pulumi-command` resource runs the Azure-specific `pgaadauth_create_principal('<identity-name>', false, false)` against the server (this single statement establishes the AAD↔role mapping that a plain `CREATE ROLE` cannot; no standard provider exposes it). It is ordered after the server + deployer-IP firewall rule.
- **Privilege grants**: `pulumi-postgresql` provider resources grant the principal the needed privileges on the `fmis` database (connect + schema/table privileges + default privileges). These are declarative Pulumi resources.
- **Authentication for both**: the provider and the command connect to the server **as the Entra admin (the CI principal) using an Entra access token** fetched in-program via `Azure.Identity` (`DefaultAzureCredential`, scope `https://ossrdbms-aad.database.windows.net/.default`) — never a password. They depend on the deployer-IP firewall rule.
- Exposes: `ClientId`, `PrincipalId`, `IdentityName`.

> "Authorization in Pulumi as far as possible": the privilege grants are pure `pulumi-postgresql` resources; the only non-provider-native piece is the single `pgaadauth_create_principal` statement, run via a `command` resource so Pulumi still orders and tracks it within `pulumi up`.

---

## Deletion protection (persistence tier)

Per [`infrastructure-tiers.md`](../../conventions/infrastructure-tiers.md), the **Flexible Server** carries both Pulumi `protect: true` and an Azure **`CanNotDelete`** management lock — a routine deploy can never tear down the database. The managed identity, firewall rules, and grants are freely recreatable (no lock).

---

## Outputs

`PersistenceStack` emits (consumed by 3b‑2 via a stack reference):

- `serverFqdn` — the Postgres FQDN.
- `databaseName` — `fmis`.
- `appIdentityClientId`, `appIdentityPrincipalId`, `appIdentityName` — the user-assigned managed identity, for 3b‑2 to assign to the compute and to build the token-auth connection (`Host=<fqdn>;Database=fmis;Username=<identity-name>;SslMode=Require`, password supplied as an Entra token at runtime).

No secret/password output.

---

## CD

Extend `.github/workflows/infra.yml` (CI option A — infra-filtered) to deploy the `persistence` stack alongside `auth`: bootstrap state → `pulumi up` the persistence stack. The job exports `DEPLOYER_IP` (the runner's egress IP, e.g. `curl -s ifconfig.me`) and `DEPLOY_PRINCIPAL_OBJECT_ID` (the deploy SP's object id) for the stack; Azure auth is the existing OIDC login, which `Azure.Identity` uses for the Postgres Entra token. PR runs `pulumi preview`; merge runs `pulumi up`. A local `pulumi preview` works when `az login`'d (export `DEPLOYER_IP`); a local `pulumi up` of the data-plane authorization additionally requires the operator to be a server Entra admin — in `dev` the CI principal owns the deploy, so CD is the authorization path.

---

## Testing (TDD)

`Pulumi.Testing` unit tests (xUnit, in `Fmis.Infra.Tests`) assert the resource graph: the server's SKU/version/Entra-only `authConfig`/PostGIS allowlist, the `fmis` database, the `AllowAllAzureServices` + deployer-IP firewall rules, the user-assigned managed identity, the presence of the principal-creation command + grant resources, the `protect` flag + `CanNotDelete` lock, and the stack outputs. The data-plane effects (the actual SQL the command/provider run) are **not** unit-tested — they require a live server and are verified by the deploy; mocks register the resources without executing them.

---

## What 3b‑2 inherits

3b‑2 reads these outputs via a stack reference: assigns `fmis-dev-app-identity` to the backend compute, sets the backend's `ConnectionStrings__Fmis` to the token-auth form (and `Auth0__Authority`/`Auth0__Audience` from the `auth` stack), changes the backend to acquire an Entra token for Postgres in Azure (keeping password auth for local docker-compose), serves the frontend, and emits `config.json`.
