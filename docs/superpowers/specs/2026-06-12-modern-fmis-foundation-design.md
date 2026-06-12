# modern-fmis Foundation — Design Spec

**Date:** 2026-06-12
**Status:** Approved for planning
**Scope:** Phase 1 of a larger Farm Management Information System (FMIS). This phase delivers a *walking skeleton* — one thin feature built end-to-end — whose purpose is to establish the architectural, development, and workflow patterns that every later phase will copy.

---

## Purpose

`modern-fmis` is a greenfield system that will eventually help farms track field operations: ingesting data from external systems (e.g., John Deere), CRUD for clients/farms/fields, crop-season planning, and field activities (planting, harvesting, applications, tillage). That full scope spans multiple independent subsystems and will be built in phases, each with its own spec → plan → implementation cycle.

**This spec covers only the foundation.** We are not building farm features yet. We are building the smallest real slice — managing **Clients** — through every layer and tool, so the patterns are demonstrated concretely rather than described abstractly. Subsequent phases copy this template.

### Success criteria

- A developer can clone the repo, run `docker compose up`, and have the full stack (Postgres+PostGIS, backend, frontend) running locally.
- The **Client** feature works end-to-end for an **authenticated** user: after logging in via Auth0, a React form creates a client; a list and detail view read them back. Unauthenticated requests are rejected (401).
- Every architectural seam, test type, and workflow gate described below exists and is exercised by the Client slice.
- CI runs on PRs and a deploy to Azure happens on merge to `main` via Pulumi.
- The result is a legible template: a developer adding the next entity (Farm) can copy the Client slice's structure without inventing new patterns.

### Explicit non-goals (deferred to later phases)

- PostGIS / spatial features. The database **is** Postgres+PostGIS, but no spatial columns, migrations, or queries are built in this phase. Spatial work begins with the Field entity in a later phase.
- The Ingestion service. Its dependency seam (`Ingestion → Core`) is part of the architecture, but **no `Fmis.Ingestion` project is created now** — an empty project is noise. It is added when its phase begins.
- Farm, Field, crop seasons, activities, external ingestion, reporting. All later phases.
- **Authorization** (roles, permissions, per-resource access rules). This phase establishes **authentication only** — verifying *who* a user is. Deciding *what* they may do is a later concern.

---

## Stack

| Concern | Choice | Reasoning |
|---|---|---|
| Backend | ASP.NET Core, REST via MVC controllers | .NET-centric org; controllers are the conventional, familiar choice (attribute routing, filters) |
| .NET runtime | Latest **LTS** (.NET 10), pinned via repo-root `global.json` | Predictable, supported runtime; pin keeps all environments on the same SDK |
| ORM | EF Core + migrations | Standard .NET data access; code-first migrations |
| Database | PostgreSQL + PostGIS (Azure-managed) | A "field" is a real land parcel; rate data is inherently spatial. PostGIS is the gold standard for geometry/raster and pays off in later phases. Azure offers a managed offering. |
| Frontend | React + TypeScript (Vite, pnpm) | Richest geospatial-mapping and data-viz ecosystem (central to a farm app later); largest talent pool |
| FE contracts | Hand-written **Zod** schemas (source of truth; `z.infer` for types) | User dislikes generated code (hard to read/review). Zod gives runtime validation at the API boundary + inferred static types. **No codegen.** |
| Cloud | Azure | Org preference / Microsoft-centric |
| IaC | Pulumi (C#) | Cross-cloud capable; supports providers beyond the big three; C# SDK keeps IaC in the same language as the backend |
| Pulumi state backend | Self-managed **Azure Storage** (`azblob://`), **not** Pulumi Cloud | Keep state in our own Azure subscription; no dependency on Pulumi's hosted service |
| Authentication | **Auth0**, configured via Pulumi in its **own stack** | Managed identity provider; authentication only (no authorization this phase). Own stack so it has an independent lifecycle and can be managed separately |
| Source host / CI/CD | GitHub + GitHub Actions | Repo is hosted on GitHub |

### Contract drift management (no codegen)

Because Zod schemas are maintained by hand and nothing auto-syncs them to the C# `Models` DTOs, drift is the risk. Mitigation that honors the no-codegen rule: the API exposes an **OpenAPI document**, and a **contract test** asserts the Zod schemas still match it. We *verify against* OpenAPI; we never *generate from* it.

---

## Architecture — Modular Monolith with Vertical Slices

Code is organized by business capability (modules), and within each module by feature slice (request → handler → data in one place). This gives per-feature readability (one feature lives in one folder) plus module seams that make later service extraction (e.g., Ingestion) clean. The module structure is kept deliberately light in the foundation — a convention, not a heavy framework.

### Dependency graph

```
Api  →  Core
Api  →  Models
(Ingestion → Core)   ← future phase; no project created now
```

- **Core** depends outward on nothing. It knows neither HTTP nor the external DTO shapes.
- **Api** depends on Core and Models.
- **Models** is a leaf (referenced by Api; not by Core).

### Project structure

```
modern-fmis/
├─ backend/                      Fmis.sln
│  ├─ src/
│  │  ├─ Fmis.Api/              HTTP ↔ Models ↔ Core features (MVC controllers)
│  │  ├─ Fmis.Core/            vertical slices + EF Core
│  │  │  ├─ Common/            interfaces, base classes, shared utilities
│  │  │  └─ Clients/           CreateClient / ListClients / GetClient
│  │  └─ Fmis.Models/          external API DTOs (mirrored by frontend Zod)
│  └─ tests/
│     ├─ Fmis.TestSupport/     NO tests — DbContext factory, fakes, shared utilities
│     ├─ Fmis.Core.Tests/      tests for Core
│     └─ Fmis.Api.Tests/       integration via Mvc.Testing / WebApplicationFactory
├─ frontend/                    React + TS + Vite + Zod (pnpm)
│  ├─ src/
│  └─ tests/
├─ infra/                       Pulumi (C#) — separate stacks
│  ├─ auth/                    Auth0 (authentication) — separately managed
│  ├─ persistence/             DB, durable storage, queues — deletion-protected
│  └─ application/             container/function apps, static-asset storage
│  (Pulumi state backend is created by a CLI step in CI, not a Pulumi stack)
├─ .github/workflows/           CI/CD
├─ docs/                        specs & design docs
└─ docker-compose.yml           postgres+postgis, backend, frontend
```

- Top level is stack-separated (backend / frontend / infra) so a polyglot repo stays legible and CI jobs can target each independently.
- Root namespace / project prefix: `Fmis`.

### Core internals

- `Core/Common`: interfaces, base classes, and shared utilities used across feature slices.
- `Core/{entity}/{feature}`: each feature slice (e.g., `Core/Clients/CreateClient`) contains its request, handler, and feature-specific types.
- Persistence (EF Core / Npgsql) lives **inside Core** (no separate Infrastructure project — matches the lean dependency graph). The accepted tradeoff: Core takes an EF Core/Npgsql dependency and is not a "pure" domain layer.
- **Feature handlers depend on the `DbContext` directly** — no repository abstraction. With no mocking frameworks and a real `DbContext` (InMemory or Testcontainers) in every test, a repository interface would add ceremony without value.
- **Slices are invoked through an in-house command/query bus** (`ICommandBus`/`IQueryBus` in `Core/Common/Messaging`), not by constructing handlers directly. The bus resolves the handler from DI by message type — no MediatR (now commercially licensed; the dispatch is small enough to own). `IEventBus` follows the same shape and is added when the first domain event exists.
- **Naming & shape conventions** (entities `*Entity`, external models `*Model`, per-operation `*Result`, generic `ListResult<T>`/`ListResultModel<T>`, handlers-via-DI, no-`new` in tests) are the repository rule in [`docs/conventions/backend-code-conventions.md`](../../conventions/backend-code-conventions.md).

### Translation responsibilities

- **Api** translates between HTTP, `Models` DTOs, and Core feature requests/results. Core features have their own internal input/output types; Api maps Core results → `Models`. This lets the external contract (and its Zod mirror) evolve independently of internal feature types.
- **Ingestion** (future) translates external data → Core features, without going through `Models`.

---

## Data flow — the Client slice

```
Auth0 login  →  React form + Zod  →  Api endpoint  →  Models DTO  →  Core slice  →  EF Core  →  Postgres
                       (Bearer token attached)         (validates JWT)
```

The slice implements three operations, enough to establish both the write-slice and read-slice patterns (Update/Delete are trivial variations to be added later by copying the template):

- **Create** — `POST /clients`
- **List** — `GET /clients`
- **Get by id** — `GET /clients/{id}`

All Client endpoints **require an authenticated user** (a valid Auth0-issued JWT). This phase establishes authentication as a pattern; it does **not** add authorization — any authenticated user can call any endpoint.

A Client in this phase is a minimal entity (e.g., id, name, and basic contact fields — exact fields finalized during planning). No spatial data.

### Authentication wiring

- **Frontend:** the React app uses the Auth0 SDK to log the user in and obtain a token, which it attaches as a Bearer token on API requests.
- **Backend:** `Fmis.Api` validates the Auth0 JWT (issuer/audience/signature) via ASP.NET Core JWT bearer authentication. Endpoints require an authenticated principal; no role/permission checks.
- **Identity resources** (Auth0 application, API/audience, connection) are provisioned by the **`auth` Pulumi stack** (see Infrastructure).

---

## Error handling

- Input validation at the boundary: Zod on the frontend (runtime + types), and request validation in the Api before reaching Core.
- The Api maps Core/validation failures to appropriate HTTP responses (e.g., 400 for validation, 404 for missing-by-id, **401 for missing/invalid authentication**). A consistent error response shape is part of the `Models` contract and is reflected in Zod.
- Core handlers return explicit results rather than relying on exceptions for expected outcomes (e.g., not-found).

---

## Testing strategy

Tests are tests — the foundation does **not** split projects by "unit" vs "integration." What matters is that tests exist and are used.

**Structure**
- One test project per production project: `Fmis.Core.Tests`, `Fmis.Api.Tests`.
- A separate `Fmis.TestSupport` project that contains **no actual tests** — it holds shared fakes, the DbContext factory, and common utilities, so tests are easy to locate and utilities are easy to share.

**Backend conventions**
- **No mocking frameworks.** Use real implementations or the EF Core InMemory provider instead of mocks.
- A simple **static DbContext factory** (in `Fmis.TestSupport`) lets any test create an EF Core context backed by either the InMemory provider or a Testcontainers Postgres instance. Each test picks what it needs. (Note: the InMemory provider cannot run PostGIS spatial queries — irrelevant this phase, relevant when spatial tests arrive.)
- `Fmis.Api.Tests` uses `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) for integration tests against the real HTTP pipeline.
- **Authentication in tests:** rather than calling real Auth0, the Api test host swaps in a **test authentication scheme** (a simple handler that issues a known principal) via `WebApplicationFactory` configuration. Tests assert both authenticated success and the **401 unauthenticated** path. (No mocking framework — this is a real, hand-written test auth handler.)

**Frontend**
- Vitest + Testing Library for component/unit tests.
- Zod schema tests.

**Cross-stack**
- One **Playwright** smoke E2E exercising the full Client path through the running stack, **including an Auth0 login** (via a dedicated test user / programmatic login). Playwright's role-based locators (`getByRole`, `getByLabel`, `getByText`, `getByTestId`) align with Testing Library queries, keeping selectors consistent with the React component tests.
- The **Zod ↔ OpenAPI contract test**.

**Discipline**
- TDD: test-first, red/green/refactor. The foundation establishes this as the working pattern.

---

## Workflow & CI/CD

- **Trunk-based development:** short-lived branches off `main`; `main` is protected.
- **PRs required:** merge requires CI green + one review.
- **CI (on PRs):** lint / format → build → test (backend + frontend) → **bootstrap Pulumi state backend (idempotent `az` CLI)** → `pulumi preview`.
- **CD (on merge to `main`):** **bootstrap Pulumi state backend (idempotent `az` CLI)** → `pulumi up` → deploy to Azure.
- **State-backend bootstrap step:** an idempotent Azure CLI step that ensures the Pulumi state storage account (and its `CanNotDelete` lock) exists. It **must run before any `pulumi preview`/`pulumi up`**, since those commands need the `azblob://` backend to already exist. Safe to re-run on every workflow execution.
- **Local dev:** `docker-compose.yml` spins up three services — Postgres+PostGIS, the backend (own Dockerfile), and the frontend (own Dockerfile) — so `docker compose up` runs the whole stack.

---

## Infrastructure (Pulumi / C#, Azure)

The `infra/` directory provisions the foundation's footprint as **separate Pulumi stacks**, each with its own lifecycle so it can be managed independently:

| Stack | Purpose | Deletion protection |
|---|---|---|
| `auth` | Auth0 identity resources (application, API/audience, connection) | n/a (config, no destructive data) |
| `persistence` | Database, durable-data storage, queues | **Yes** |
| `application` | Container/Function apps, static-asset storage, CI/CD identity wiring | No |

The `persistence` and `application` stacks follow the **two-tier deletion-protection policy** below — the repository rule in [`docs/conventions/infrastructure-tiers.md`](../../conventions/infrastructure-tiers.md) is the source of truth (summarized here for context).

The Pulumi **state backend** storage account is *not* one of these stacks — see below.

### Pulumi state backend (CI/CD bootstrap step)

Pulumi state is **not** stored in Pulumi's hosted service. Instead, a self-managed **Azure Storage account** holds the state files, and all stacks use the `azblob://` backend (`pulumi login azblob://...`).

This account is **not provisioned by Pulumi** (it would have nowhere to store its own state — chicken-and-egg). Instead, a **CI/CD step using the Azure CLI creates it idempotently** (e.g., `az group create` / `az storage account create` / `az storage container create`, all safe to re-run) **before any `pulumi preview` or `pulumi up` runs** in the workflow. The same step idempotently applies an Azure `CanNotDelete` lock, since the account holds state for everything. Being idempotent, the step is a harmless no-op on every run after the first.

### Authentication stack (Auth0)

The `auth` stack provisions the Auth0 resources needed for authentication: the application/client the React frontend logs into, the API (audience) the backend validates tokens against, and the user connection. Kept as its own stack so identity changes are deliberate and decoupled from application deploys. Authentication only — no roles/permissions/authorization resources this phase.

### Two tiers

**1. Persistence infrastructure — strict deletion prevention.**
Resources that hold durable, hard-or-impossible-to-recreate state:
- The managed PostgreSQL (PostGIS-capable) database.
- Storage accounts that hold **durable user/business data** (videos, zip files, uploaded documents, etc.).
- Queues and similar stateful messaging infrastructure.

These get strict deletion prevention applied: Pulumi resource `protect` **and** an Azure `CanNotDelete` management lock (defense in depth — Pulumi guards against `pulumi destroy`, the Azure lock guards against deletion outside Pulumi). Routine application deploys must never be able to tear these down.

**2. Application infrastructure — no strict deletion prevention.**
Stateless, freely re-creatable compute and its supporting resources:
- Container Apps / Function Apps (backend API, future functions).
- Storage accounts that hold **static web assets only** (html / css / js).
- Other compute/config that can be rebuilt from source on the next deploy.

These deploy and redeploy freely without deletion locks.

### The storage-account classification rule

Storage accounts are classified by **what they hold, not by being storage accounts**:
- Static frontend assets (html/css/js) → **application** tier, no deletion protection.
- Durable data (videos, zips, uploads, anything not regenerable from source) → **persistence** tier, deletion protection.

A given phase may therefore provision storage accounts in *both* tiers.

### Structure & lifecycle

The two tiers are separated in the Pulumi codebase (separate stacks/projects, or clearly separated component resources — finalized in planning) so the persistence tier has its own lifecycle and a routine application deploy never touches it. CI/CD deploys the application tier on every merge; the persistence tier changes only via deliberate, reviewed runs.

### This phase's minimum footprint

- **State backend (CLI bootstrap, not a stack):** Azure Storage account/container for Pulumi state + `CanNotDelete` lock, created idempotently by CI before any Pulumi run.
- **`auth`:** Auth0 application + API (audience) + connection.
- **`persistence`:** managed PostgreSQL (PostGIS-capable) with deletion prevention.
- **`application`:** hosting for the backend API, hosting/serving for the frontend (incl. a static-asset storage account if used), plus the CI/CD identity/secret wiring the GitHub Actions deploy requires.

Exact compute selection (e.g., Container Apps vs. App Service vs. Function Apps) is a planning-phase decision.

---

## Open questions (resolve during planning)

- Exact Client entity fields.
- Azure compute choice for backend/frontend (App Service vs. Container Apps vs. other) and how that interacts with the Docker-based local setup.
- Exact Azure CLI commands / naming for the state-backend bootstrap step (mechanism is decided: idempotent `az` CLI in CI before any Pulumi run).
- Auth0 specifics: tenant/environment strategy (per-env tenants vs. shared), and how the Playwright E2E obtains a token (dedicated test user vs. programmatic login).
- Consistent error-response shape details.

---

## What later phases inherit from this foundation

Farm → Field → crop seasons → field activities (planting/harvest/applications/tillage) → external ingestion (John Deere) → reporting. Each is its own spec → plan → build cycle, copying the slice/module/test/workflow patterns established here. The Ingestion module gets its project when its phase starts; PostGIS/spatial work begins with Field.
