# Repository Rule: Backend Code Conventions

**Applies to:** all backend C# code (`Fmis.*`), every phase.

## Naming postfixes

Make a type's role obvious from its name:

| Kind | Postfix | Example | Lives in |
|---|---|---|---|
| EF Core entity | `Entity` | `ClientEntity` | `Fmis.Core/<Area>/` |
| External API model (DTO) | `Model` | `CreateClientRequestModel`, `ClientResponseModel` | `Fmis.Models/` |
| Core operation result | `Result` | `CreateClientResult` (per-op), `ClientResult` (shared read) | `Fmis.Core/<Area>/<Feature>/` or `Fmis.Core/<Area>/` |
| Core command | `Command` | `CreateClientCommand` | `Fmis.Core/<Area>/<Feature>/` |
| Core query | `Query` | `GetClientQuery`, `ListClientsQuery` | `Fmis.Core/<Area>/<Feature>/` |

## Result types

**Reads of an entity share a canonical `<Entity>Result`** (e.g. `ClientResult`): get-by-id returns it singular, list returns `ListResult<ClientResult>`. The shared read result lives at the **area** level (`Fmis.Core/<Area>/`), not in a feature folder, since multiple read features use it. Don't create a separate identical `GetXResult` / `ListXResult` per read — that's duplication.

**Commands (and any operation whose output genuinely differs) define their own `<Operation>Result`** (e.g. `CreateClientResult`), living in the feature folder. Don't force a command to return the shared read result.

If several operation results later need to share fields, introduce a shared base **at that point**, named for what it represents, and have them inherit it — don't pre-create it (YAGNI).

## Collections: generic list results

Lists use generic wrappers carrying the page of items plus the total count of the full result set (forward-compatible with paging):

- Core: `ListResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount)` — in `Fmis.Core/Common/`.
- Models: `ListResultModel<TModel>(IReadOnlyList<TModel> Items, int TotalCount)` — in `Fmis.Models/Common/`.

A list query returns `ListResult<TItem>`; the Api maps it to `ListResultModel<TModel>`.

## Messaging: in-house command/query bus

Slices are invoked through a thin in-house bus — **no MediatR** (it is now commercially licensed; the dispatch code is small enough to own). Interfaces live in `Fmis.Core/Common/Messaging/`:

- `ICommand<TResult>` + `ICommandHandler<TCommand, TResult>` + `ICommandBus.ExecuteAsync(command, ct)`.
- `IQuery<TResult>` + `IQueryHandler<TQuery, TResult>` + `IQueryBus.QueryAsync(query, ct)`.
- The bus resolves the closed handler type from `IServiceProvider` and dispatches. Handlers depend on `FmisDbContext` directly.
- **Handlers are auto-registered by reflection.** `AddMessaging(params Assembly[])` discovers every `ICommandHandler<,>` / `IQueryHandler<,>` implementation in the given assemblies and registers it — never register handlers one-by-one.
- **The command bus validates first.** Before dispatch, `CommandBus` resolves an optional `IValidator<TCommand>` (FluentValidation) and throws `ValidationException` on failure; the Api maps that to a 400 via an `IExceptionHandler`. Validators live with their slice and are registered by assembly scan. Queries are not validated.
- `IEventBus` (publish to many `IEventHandler<TEvent>`) follows the same shape and is added **when the first domain event exists** — not before.

**Handlers are never constructed directly in production or test code.** Callers depend on `ICommandBus` / `IQueryBus`, never on a concrete handler.

**Validation library:** FluentValidation (free/open-source). Cross-field rules (e.g. "email or phone required") belong in the slice's validator.

## Api host composition

`Program.cs` stays thin — it only builds the host, adds services, runs the pipeline, and calls `Run()`. Everything else lives in extension methods under `Fmis.Api/Configuration/`:

- **Service registration** → `IServiceCollection` extensions (e.g. `AddApiServices`, with cohesive sub-methods like `AddApiErrorHandling`, `AddApiDocumentation`, `AddApiAuthentication`).
- **Request pipeline / startup** → `WebApplication` extensions (e.g. `UseApiPipeline`, `MapApiEndpoints`, `MigrateDatabase`). `MapApiEndpoints` is the one place feature endpoint groups are wired. `MigrateDatabase` guards on `Database.IsRelational()` so it no-ops under the InMemory provider used by tests.

When a new cross-cutting concern is added, extend or add an extension method — don't grow `Program.cs`.

## Testing: exercise through DI, not `new`

Handler/slice tests resolve `ICommandBus` / `IQueryBus` from a **real DI container** (the same composition the Api uses) and execute messages through them. Never `new` a handler in a test.

Slice tests inherit **`InMemoryCoreTestBase`** (in `Fmis.TestSupport`), which owns the DI container + a single scope and exposes `CommandBus` / `QueryBus` / `Db` — so a test body has no scope/setup boilerplate. It implements `IDisposable`; xUnit disposes the test instance (and thus the scope) after each test. `InMemoryCoreTestBase` builds on `TestServices.CreateInMemory()`, which backs the container with an InMemory `FmisDbContext`. A sibling `ContainerCoreTestBase` (Testcontainers-backed) is added when a test first needs real Postgres (e.g. PostGIS/spatial work); the two share a common abstract base at that point. No mocking frameworks — use real implementations or the InMemory provider.

**Seed through the application, not the DbContext.** Create test data by executing the real commands through the bus (e.g. `await CommandBus.ExecuteAsync(new CreateClientCommand(...))`), so tests exercise the true write path and its validation. Reserve direct `Db` access for assertions, or for state a command cannot yet produce.
