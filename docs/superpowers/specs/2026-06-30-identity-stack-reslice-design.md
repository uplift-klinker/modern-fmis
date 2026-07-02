# modern-fmis Identity Stack Re-slice (Phase 3b) — Design Spec

**Date:** 2026-06-30
**Status:** Approved for planning
**Scope:** A re-slice of the Phase 3b infrastructure that introduces a dedicated `identity` stack, moves the ACR into the `persistence` tier, and reworks the `application` stack to push via identity (no ACR admin). Lands on the `application-stack` branch as part of Phase 3b‑2 (before merge). Supersedes the ACR ownership + admin-based push described in [`2026-06-30-application-stack-design.md`](2026-06-30-application-stack-design.md).

---

## Purpose

Resolve the container-image push authentication cleanly and centralize identity management. The current 3b‑2 implementation creates the ACR inside the `application` stack and enables ACR **admin** so the same `pulumi up` that creates the registry can push to it (a chicken-and-egg workaround). Instead:

- **The ACR moves into `persistence`** — it is durable, created once per environment, and holds every pushed image, so it belongs in the deletion-protected persistence tier (per [`infrastructure-tiers.md`](../../conventions/infrastructure-tiers.md)), not the freely-redeployable application tier.
- **A new `identity` stack owns the managed identity and every permission it holds** — so downstream stacks simply *consume* the identity. Because the ACR (in `persistence`) then pre-exists before `application` runs, the image push authenticates via the **CD deploy principal's `AcrPush`** with **ACR admin disabled** — no chicken-and-egg.

### Success criteria

- `infra/` builds; all `Fmis.Infra.Tests` (persistence, identity, application) green; backend + contract tests unchanged.
- ACR admin is **disabled**; the backend image is built + pushed by Pulumi during the `application` deploy, authenticating with the deploy principal's `AcrPush` (granted by `identity`) against the pre-existing ACR.
- The managed identity and all its role assignments (`AcrPull`, `AcrPush`, the Postgres role) live in the `identity` stack.
- The ACR carries deletion protection (`protect` + `CanNotDelete`).

### Non-goals (unchanged)

- Phase 3c (live login + Playwright); `CREATE EXTENSION postgis` (Field phase); custom domains/CDN/private networking; multiple environments (single `dev`, additive).

---

## Stack map (after the re-slice)

| Stack | Owns | Consumes |
|---|---|---|
| `auth` | Auth0 SPA + API + tenant (unchanged) | — |
| `persistence` | Postgres server + `fmis` db **+ ACR** (all deletion-protected). **No identity.** | — |
| `identity` (new) | the user-assigned managed identity; role assignments `AcrPull` (identity→ACR) + `AcrPush` (deploy principal→ACR); the Postgres role (`pgaadauth` principal + grants) | `persistence` (ACR + server) |
| `application` | Container App + frontend static site + `config.json`; builds/pushes the backend image | `auth`, `persistence` (ACR + db), `identity` (the identity) |

**CD order:** `auth → persistence → identity → application`.

---

## What moves

**Into `persistence`:**
- A `ContainerRegistry` component: ACR **Basic**, **`AdminUserEnabled = false`**, with `protect: true` + a `CanNotDelete` management lock (like the Postgres server).
- New outputs: `acrLoginServer`, `acrId`, `acrName`.

**Out of `persistence` → into `identity`:**
- The `DatabaseIdentity` work (the user-assigned managed identity, the `pgaadauth_create_principal` command, and the `pulumi-postgresql` grants) moves wholesale to the `identity` stack, which references `persistence`'s `serverFqdn` and runs the same in-Pulumi authorization (token seam + deployer-IP firewall reads still apply). `persistence` no longer emits `appIdentity*`.

**Removed from `application`:**
- The `ContainerRegistry` component and the `ListRegistryCredentials` + admin-based push auth. The `application` stack references the ACR from `persistence` and the identity from `identity`.

---

## The `identity` stack (new)

`IdentityStack` (thin root) over:
- **`AppIdentity`** — the `UserAssignedIdentity` (`fmis-dev-app-identity`). Exposes `ClientId`, `PrincipalId`, `Name`, `ResourceId`.
- **`RegistryAccess`** — two `RoleAssignment`s on the ACR scope (from `persistence`): `AcrPull` for the identity's `principalId`, and `AcrPush` for the CD deploy principal (`DEPLOY_PRINCIPAL_OBJECT_ID`). Role-assignment names are deterministic GUIDs derived from `scope|principalId|role`.
- **`DatabaseAccess`** — the `pgaadauth` principal (`pulumi-command`) + the `pulumi-postgresql` grants for the identity on the `fmis` db, authenticated as the Entra admin via the `Azure.Identity` token seam (moved from persistence), referencing `persistence`'s `serverFqdn`.

Reads `persistence` via a stack reference (`acrId`, `serverFqdn`, `databaseName`). Naming `ResourceNames.For(env, "identity", …)`. Emits `appIdentityClientId`/`appIdentityPrincipalId`/`appIdentityName`/`appIdentityResourceId`.

---

## The `application` rework

- Drop the `ContainerRegistry` component + `ListRegistryCredentials`.
- Read the ACR (`acrLoginServer`) from `persistence` and the identity ids from `identity` (two stack references, alongside `auth`).
- The `BackendApp` uses `identity.appIdentityResourceId` (assigned identity + registry pull identity) — no more constructing the resource id from `GetClientConfig`; the `identity` stack emits it directly.
- The Pulumi `docker-build` image pushes to the pre-existing ACR with **only the `Address`** (no `Registries` credentials); the **CD `apps` job runs `az acr login --name <acr>`** first (the deploy principal has `AcrPush` from `identity`) so the push uses the ambient docker credential. **Admin off.**
- Container App + frontend + `config.json` otherwise unchanged.

---

## CD

`infra-deploy.yml` (the infra-tier reusable) deploys `auth → persistence → identity` (add the `identity` stack after `persistence`, reading `DEPLOYER_IP`/`DEPLOY_PRINCIPAL_OBJECT_ID`/`AZURE_TENANT_ID` as `identity` now runs the pgaadauth provisioning). The `apps` job (application) `needs: [infra]`, adds `az acr login --name fmis<env>acr` after `azure/login`, then `pnpm build` + `pulumi up`.

---

## Migration note (dev)

`persistence` is deployed on `main`. This re-slice removes the identity + Postgres grant from `persistence`'s state (they are recreated by `identity`) and adds the ACR. On the next deploy, Pulumi deletes the identity from `persistence` and `identity` recreates it; the deletion-protected Postgres **server + database are untouched**. Acceptable for `dev`; the brief identity recreation has no data impact. The whole re-slice ships together (persistence + identity + application deployed in order), so the identity is recreated in the same CD run that removes it.

---

## Testing (TDD)

`Pulumi.Testing` per stack: `persistence` gains ACR + protection assertions and drops the identity assertions; `identity` (new `IdentityStackTests`) asserts the UAMI, the two ACR role assignments, and the `pgaadauth` command + grants, with `persistence` mocked via a stack reference; `application` drops the ACR/registry-credentials assertions and asserts it consumes the ACR + identity from stack references. The data-plane effects (image push, `pgaadauth` SQL, asset upload) remain deploy-verified, not unit-tested.
