# Architecture & Stack

The durable architectural decisions for modern-fmis, extracted from the phase-1 spec/plan so they apply to every phase. Dated specs/plans under `docs/superpowers/` are the per-phase record; this doc is the standing reference. Companion rules: [`backend-code-conventions.md`](backend-code-conventions.md), [`test-driven-development.md`](test-driven-development.md), [`git-workflow.md`](git-workflow.md), [`infrastructure-tiers.md`](infrastructure-tiers.md).

## What the system is

A Farm Management Information System: track field operations — clients/farms/fields CRUD, crop-season planning, field activities (planting, harvest, applications, tillage), ingestion from external systems (e.g. John Deere), and reporting.

**Built in phases**, each its own spec → plan → build cycle, each copying the patterns the foundation established. Roadmap:

> Client (foundation) → Farm → Field (PostGIS/spatial begins here) → crop seasons → field activities → external ingestion (John Deere) → reporting.

The `Ingestion` module/project is created when its phase starts (no empty stubs before then).

## Monorepo layout

One repository holds everything — app code and IaC — stack-separated at the top level so a polyglot repo stays legible and CI can target each part independently:

```
Fmis.slnx   single root solution — backend + infra projects, frontend as a solution folder
backend/    .NET 10 backend projects (modular monolith)
frontend/   React + TypeScript (Vite, pnpm)   [from its phase]
infra/      Pulumi (C#) — Azure, separate stacks [from its phase]
docs/       specs, plans, conventions
.github/workflows/   CI/CD
docker-compose.yml   full local stack
```

The root `Fmis.slnx` spans all .NET projects (`backend/` + `infra/`) so they are built and tested with a single `dotnet` invocation from the repo root. The `frontend/` directory appears as a non-buildable solution folder (navigation only — editors surface the config files; MSBuild ignores them). Stack directories stay unchanged; the single solution is purely an organizational layer.

## Backend architecture — modular monolith, vertical slices

Code is organized by business capability (modules), and within each module by **feature slice** (request → handler → data in one folder). One feature lives in one place (readability); module seams keep future service extraction (e.g. Ingestion) clean. Kept light — a convention, not a framework.

### Dependency rule

```
Api → Core,  Api → Models
Ingestion → Core            (future)
```

- **Core** depends outward on nothing — it knows neither HTTP nor the external DTO shapes.
- **Models** is a leaf (the external API contract; referenced by Api, never by Core).
- **Api** translates HTTP ↔ `Models` ↔ Core messages, and maps Core results → `Models` (so the external contract evolves independently of internal feature types).

### Inside Core

- `Core/Common/` — shared interfaces, base classes, and the messaging bus (`Common/Messaging`).
- `Core/<Area>/<Feature>/` — one folder per feature (e.g. `Clients/CreateClient`): command/query, result, validator, handler.
- **Persistence (EF Core / Npgsql) lives inside Core** — no separate Infrastructure project. Accepted tradeoff: Core takes an EF/Npgsql dependency and isn't a "pure" domain layer.
- **Handlers depend on `DbContext` directly** — no repository abstraction (tests use a real InMemory/Testcontainers context, so an interface adds only ceremony).
- **Slices are invoked through an in-house command/query bus** (`ICommandBus`/`IQueryBus`) — never by constructing handlers. The command bus validates (FluentValidation) before dispatch; handlers are auto-discovered by reflection. No MediatR. Full rules: [`backend-code-conventions.md`](backend-code-conventions.md).

### At the edge

The HTTP layer is **MVC controllers** (`[ApiController]`, attribute routing); `Program.cs` is thin, delegating to service/pipeline extension methods under `Fmis.Api/Configuration/`.

### Solution & projects

Root `Fmis.slnx`, root namespace/prefix `Fmis`:

```
backend/src/   Fmis.Api · Fmis.Core · Fmis.Models   (+ Fmis.Ingestion later)
backend/tests/ Fmis.TestSupport (no tests; shared utilities) · Fmis.Core.Tests · Fmis.Api.Tests
infra/         Fmis.Infra.Common · Fmis.Infra.Auth · Fmis.Infra.Tests
```

## Tech stack (with reasoning)

| Concern | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (latest **LTS**), pinned via root `global.json` | Supported, reproducible; run .NET CLI from the repo root so the pin applies |
| Backend | ASP.NET Core, REST via **MVC controllers** | Conventional, familiar (attribute routing, filters) |
| ORM / DB | EF Core (code-first migrations) + **PostgreSQL + PostGIS** (Azure-managed) | A field is a real land parcel; rate data is spatial — PostGIS pays off from the Field phase on |
| Validation | **FluentValidation** (in the command bus) | Expressive cross-field rules; free/open-source |
| Frontend | **React + TypeScript** (Vite, pnpm) | Richest geospatial-mapping/data-viz ecosystem; largest talent pool |
| FE contracts | Hand-written **Zod** schemas (`z.infer` for types) — **no codegen** | Generated code is hard to read/review; Zod gives runtime validation + inferred types. Drift is caught by a contract test that verifies Zod against the API's OpenAPI document — verify against, never generate from |
| Auth | **Auth0** (JWT bearer) — **authentication only** (no authorization yet) | Managed identity; provisioned by its own Pulumi stack |
| Cloud / IaC | **Azure** + **Pulumi (C#)** | Org is Microsoft-centric; Pulumi is cross-cloud and keeps IaC in the backend's language |
| Pulumi state | Self-managed **Azure Storage** (`azblob://`), not Pulumi Cloud | State stays in our subscription; created by an idempotent `az` CLI step in CI before any Pulumi run |
| Source / CI/CD | **GitHub** + GitHub Actions | Repo host |

## Authentication

Auth0 JWT bearer, **authentication only** — verify *who* a user is; *what* they may do (authorization: roles/permissions) is a later concern. Backend validates the Auth0 JWT (issuer/audience/signature) via ASP.NET Core JWT bearer; endpoints require an authenticated principal. Frontend uses the Auth0 SDK and attaches a Bearer token. In tests, a hand-written test authentication scheme replaces Auth0 (assert both success and the 401 path) — no mocking framework.

## Error handling

Validate at the boundary (Zod on the frontend; the command bus on the backend). The Api maps failures to HTTP: 400 validation, 404 not-found, 401 unauthenticated. Core handlers return explicit results (e.g. `null` for not-found) rather than throwing for expected outcomes.

## Workflow & CI/CD

- **Trunk-based**: short-lived branches off protected `main`; merge needs CI green + one review. New commits only — never amend/force-push ([`git-workflow.md`](git-workflow.md)).
- **TDD always** ([`test-driven-development.md`](test-driven-development.md)).
- **CI (PRs):** lint/format → build → test (backend + frontend) → bootstrap Pulumi state backend (idempotent `az` CLI) → `pulumi preview`.
- **CD (merge to `main`):** bootstrap state backend → `pulumi up` → deploy to Azure.
- **Local:** `docker compose up` runs the whole stack (Postgres+PostGIS, backend, frontend).
- Tooling is **local, not global** (`.config/dotnet-tools.json`).

## Infrastructure

Azure, provisioned by Pulumi (C#) as **separate stacks** with independent lifecycles: `auth` (Auth0 resources), `persistence` (DB, durable storage, queues — deletion-protected), `application` (compute, static-asset storage). The Pulumi **state backend** storage account is created by a CLI step in CI, not a Pulumi stack. Deletion-protection tiers (persistence vs application, with the storage-by-content classification rule) are the repository rule in [`infrastructure-tiers.md`](infrastructure-tiers.md).

Pulumi stacks are composed from small, single-purpose **`ComponentResource`s** (one concern each, exposing typed outputs); the stack class is a thin composition root that wires component outputs to stack outputs — no monolithic stack constructors.

Auth0/provider credentials (`AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_CLIENT_SECRET`) are supplied via **environment variables** in CI/CD and local runs — never written to committed Pulumi config files. Only non-secret stack settings (e.g. `enableE2eUser`) live in `Pulumi.<stack>.yaml`.

The default Azure region for all resources is **`centralus`** (overridable via `AZURE_LOCATION`).

## Testing

TDD-first ([`test-driven-development.md`](test-driven-development.md)); how tests are written (no mocks, through the bus/HTTP, seeded via commands, `InMemoryCoreTestBase`, one test project per production project + `Fmis.TestSupport`) is in [`backend-code-conventions.md`](backend-code-conventions.md). Cross-stack: a Playwright smoke E2E (incl. Auth0 login) and the Zod↔OpenAPI contract test.
