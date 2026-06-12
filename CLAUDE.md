# modern-fmis — Working Agreement

A Farm Management Information System. Greenfield, built in phases (see `docs/superpowers/specs/` and `docs/superpowers/plans/`). Backend: .NET 10 modular monolith under `backend/`. Frontend (React/TS) and infra (Pulumi/Azure) arrive in later phases.

## Test-Driven Development is mandatory

**Write a failing test before the production code that makes it pass. Always. Every layer.**

Red → green → refactor: write the smallest failing test → run it and confirm it fails for the expected reason → write the minimum code to pass → run it green → refactor. Never write a handler, controller, validator, or any production type before its test exists and has been seen to fail. This includes **Api controllers/endpoints** — write the failing integration test through the real HTTP pipeline *first*, then the controller. "Implemented it, then added tests" is a violation.

Full rule: [`docs/conventions/test-driven-development.md`](docs/conventions/test-driven-development.md).

## Conventions (read these — they are rules, not suggestions)

- [`docs/conventions/test-driven-development.md`](docs/conventions/test-driven-development.md) — TDD (above).
- [`docs/conventions/backend-code-conventions.md`](docs/conventions/backend-code-conventions.md) — `*Entity`/`*Model`/`*Result` naming; shared read results; generic `ListResult`/`ListResultModel`; in-house command/query bus (no MediatR) with reflection discovery and command validation; MVC controllers; thin `Program.cs` with service/pipeline extensions; **no code comments** (self-documenting names instead); tests through the bus/HTTP with no mocks, seeded via commands; local dotnet tools; `InMemoryCoreTestBase`.
- [`docs/conventions/git-workflow.md`](docs/conventions/git-workflow.md) — append-only history: **new commits only, never amend or force-push.**
- [`docs/conventions/infrastructure-tiers.md`](docs/conventions/infrastructure-tiers.md) — Pulumi deletion-protected persistence tier vs. freely-redeployable application tier.

## Toolchain notes

- The .NET SDK is pinned by `backend/global.json`. Run .NET CLI commands from `backend/` so the pin applies (e.g. `cd backend && dotnet test Fmis.slnx`). The solution file is `backend/Fmis.slnx`.
- `bin/` and `obj/` are build output — never read or edit them.
