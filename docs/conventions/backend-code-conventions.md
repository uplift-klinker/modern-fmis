# Repository Rule: Backend Code Conventions

**Applies to:** all backend C# code (`Fmis.*`), every phase.

## Naming postfixes

Make a type's role obvious from its name:

| Kind | Postfix | Example | Lives in |
|---|---|---|---|
| EF Core entity | `Entity` | `ClientEntity` | `Fmis.Core/<Area>/` |
| External API model (DTO) | `Model` | `CreateClientRequestModel`, `ClientResponseModel` | `Fmis.Models/` |
| Core operation result | `Result` | `CreateClientResult`, `GetClientResult` | `Fmis.Core/<Area>/<Feature>/` |
| Core command | `Command` | `CreateClientCommand` | `Fmis.Core/<Area>/<Feature>/` |
| Core query | `Query` | `GetClientQuery`, `ListClientsQuery` | `Fmis.Core/<Area>/<Feature>/` |

## Per-operation result types

Each operation returns its **own** result type (`CreateClientResult`, `GetClientResult`, …) — not a single shared "ClientResult". This keeps each slice's contract independent.

If two results genuinely need to share fields, introduce a shared base **at that point**, named for what it represents (not `*Result`), and have the operation results inherit it. Do not pre-create the base (YAGNI).

## Collections: generic list results

Lists use generic wrappers carrying the page of items plus the total count of the full result set (forward-compatible with paging):

- Core: `ListResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount)` — in `Fmis.Core/Common/`.
- Models: `ListResultModel<TModel>(IReadOnlyList<TModel> Items, int TotalCount)` — in `Fmis.Models/Common/`.

A list query returns `ListResult<TItem>`; the Api maps it to `ListResultModel<TModel>`.

## Messaging: in-house command/query bus

Slices are invoked through a thin in-house bus — **no MediatR** (it is now commercially licensed; the dispatch code is small enough to own). Interfaces live in `Fmis.Core/Common/Messaging/`:

- `ICommand<TResult>` + `ICommandHandler<TCommand, TResult>` + `ICommandBus.ExecuteAsync(command, ct)`.
- `IQuery<TResult>` + `IQueryHandler<TQuery, TResult>` + `IQueryBus.QueryAsync(query, ct)`.
- The bus resolves the closed handler type from `IServiceProvider` and dispatches. Handlers are registered in DI and depend on `FmisDbContext` directly.
- `IEventBus` (publish to many `IEventHandler<TEvent>`) follows the same shape and is added **when the first domain event exists** — not before.

**Handlers are never constructed directly in production or test code.** Callers depend on `ICommandBus` / `IQueryBus`, never on a concrete handler.

## Testing: exercise through DI, not `new`

Handler/slice tests resolve `ICommandBus` / `IQueryBus` from a **real DI container** (the same composition the Api uses) and execute messages through them. Never `new` a handler in a test. The shared `TestServices.CreateInMemory()` builds that container with an InMemory `FmisDbContext`. (Persistence/schema tests may use a raw `FmisDbContext` from the `TestDb` factory.) No mocking frameworks — use real implementations or the InMemory provider.
