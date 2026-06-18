# modern-fmis — Working Agreement

A Farm Management Information System. Greenfield, built in phases (see `docs/superpowers/specs/` and `docs/superpowers/plans/`). Backend: .NET 10 modular monolith under `backend/`. Frontend (React/TS) and infra (Pulumi/Azure) arrive in later phases.

## Test-Driven Development is mandatory

Follow Robert C. Martin's **Three Laws of TDD** (<http://butunclebob.com/ArticleS.UncleBob.TheThreeRulesOfTdd>):

1. No production code except to make a failing unit test pass.
2. No more of a test than is sufficient to fail — a compilation failure is a failure.
3. No more production code than is sufficient to pass the one failing test.

This is a tight, seconds-long cycle: a fragment of a failing test → just enough code to pass → refactor. Never write a handler, controller, validator, or any production type before its test exists and has been seen to fail. This includes **Api controllers/endpoints** — write the failing integration test through the real HTTP pipeline *first*, then the controller. "Implemented it, then added tests" is a violation.

Full rule: [`docs/conventions/test-driven-development.md`](docs/conventions/test-driven-development.md).

## Architecture (one-paragraph orientation)

Modular monolith, vertical slices. Backend dependency rule: `Api → Core, Models`; **Core depends outward on nothing** (no HTTP, no DTO knowledge); `Models` is the external contract (leaf). Inside Core, one folder per feature (command/query, result, validator, handler); persistence (EF Core/Npgsql) lives in Core; handlers depend on `DbContext` directly and are invoked via an in-house command/query bus (the command bus validates first). MVC controllers at the edge; thin `Program.cs`. Stack: .NET 10, PostgreSQL+PostGIS, FluentValidation, React/TS + hand-written Zod (no codegen), Auth0 (authentication only), Azure + Pulumi (C#). Full detail + reasoning + phased roadmap: [`docs/conventions/architecture.md`](docs/conventions/architecture.md).

## Conventions (read these — they are rules, not suggestions)

- [`docs/conventions/architecture.md`](docs/conventions/architecture.md) — architecture, stack (with reasoning), monorepo layout, auth, infra, roadmap.
- [`docs/conventions/test-driven-development.md`](docs/conventions/test-driven-development.md) — TDD (above).
- [`docs/conventions/backend-code-conventions.md`](docs/conventions/backend-code-conventions.md) — `*Entity`/`*Model`/`*Result` naming; shared read results; generic `ListResult`/`ListResultModel`; in-house command/query bus (no MediatR) with reflection discovery and command validation; MVC controllers; thin `Program.cs` with service/pipeline extensions; **no code comments** (self-documenting names instead); tests through the bus/HTTP with no mocks, seeded via commands; local dotnet tools; `InMemoryCoreTestBase`.
- [`docs/conventions/frontend-conventions.md`](docs/conventions/frontend-conventions.md) — React/TS + Vite, Node 24 LTS + pnpm (Corepack); MUI; Redux Toolkit + RTK Query; Auth0; hand-written Zod (no codegen); **build-once + runtime `config.json`** (Zod-validated before render); React Router with an auth guard that preserves deep-link `returnTo`; feature-sliced production code, **centralized `src/testing/`** harness (`renderWithProviders`, MSW-backed `TestingApiServer`, `RequestCapture`, faker `ModelFactory`); behavior-first tests (role/label queries, stub only the network edge, no snapshots/self-mocking).
- [`docs/conventions/git-workflow.md`](docs/conventions/git-workflow.md) — append-only history: **new commits only, never amend or force-push.**
- [`docs/conventions/infrastructure-tiers.md`](docs/conventions/infrastructure-tiers.md) — Pulumi deletion-protected persistence tier vs. freely-redeployable application tier.

## Toolchain notes

- The .NET SDK is pinned by the root `global.json`. Run .NET CLI commands from the **repo root** — the pin applies automatically (e.g. `dotnet test Fmis.slnx`). The solution file is the root `Fmis.slnx`.
- `bin/` and `obj/` are build output — never read or edit them.
