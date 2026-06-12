# Backend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working, authenticated ASP.NET Core API for managing `Client` records end-to-end (Create / List / Get-by-id), establishing the modular-monolith + vertical-slice + in-house-bus + TDD patterns that every later phase copies.

**Architecture:** Modular monolith with vertical slices. `Fmis.Api` (HTTP) → `Fmis.Core` (vertical slices + EF Core) and `Fmis.Models` (external DTOs). Core knows nothing about HTTP or external DTOs; the Api translates between them. Slices are invoked through an in-house command/query **bus** resolved from DI — handlers are never constructed directly. Persistence (EF Core / Npgsql) lives inside Core; handlers depend on `FmisDbContext` directly (no repository abstraction, no mocking frameworks). Authentication only (Auth0 JWT bearer) — no authorization rules.

**Tech Stack:** .NET 10 — the current LTS (`net10.0`), pinned via a repo-root `global.json` — ASP.NET Core minimal APIs, EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL`, PostgreSQL+PostGIS (via Docker), xUnit, `Microsoft.AspNetCore.Mvc.Testing`, EF Core InMemory provider, `Testcontainers.PostgreSql`, built-in `Microsoft.AspNetCore.OpenApi`.

**Conventions (from `docs/conventions/`):** New commits only — never amend or force-push. `*Entity` for EF entities, `*Model` for external DTOs, per-operation `*Result` types, generic `ListResult<T>` / `ListResultModel<T>`. In-house command/query bus — no MediatR, no direct handler construction. No mocking frameworks. Tests exercise slices through the bus resolved from a real DI container. One test project per production project, plus a no-tests `Fmis.TestSupport`.

**Out of scope (later plans):** React frontend, Zod, Auth0 login UI, Playwright (Plan 2). Pulumi stacks, CI/CD, Azure deploy (Plan 3). PostGIS/spatial columns, the Ingestion project, and `IEventBus`/domain events (later phases). The DB image is PostGIS-capable but no spatial features are used here.

---

## File Structure

```
global.json                                   pins .NET SDK to the latest LTS (10.x)
backend/
├─ Fmis.sln
├─ src/
│  ├─ Fmis.Core/
│  │  ├─ Fmis.Core.csproj
│  │  ├─ FmisDbContext.cs                     EF Core context (DbSet<ClientEntity>)
│  │  ├─ FmisDbContextFactory.cs              design-time factory for migrations
│  │  ├─ CoreServiceCollectionExtensions.cs   AddFmisCore / AddFmisCoreHandlers
│  │  ├─ Common/
│  │  │  ├─ ListResult.cs                     generic ListResult<TItem>
│  │  │  └─ Messaging/
│  │  │     ├─ ICommand.cs  ICommandHandler.cs  ICommandBus.cs  CommandBus.cs
│  │  │     ├─ IQuery.cs    IQueryHandler.cs    IQueryBus.cs    QueryBus.cs
│  │  │     └─ MessagingServiceCollectionExtensions.cs
│  │  ├─ Migrations/                          EF migrations (generated)
│  │  └─ Clients/
│  │     ├─ ClientEntity.cs                   entity
│  │     ├─ ClientConfiguration.cs            IEntityTypeConfiguration<ClientEntity>
│  │     ├─ CreateClient/
│  │     │  ├─ CreateClientCommand.cs         : ICommand<CreateClientResult>
│  │     │  ├─ CreateClientResult.cs
│  │     │  └─ CreateClientHandler.cs         : ICommandHandler<...>
│  │     ├─ ListClients/
│  │     │  ├─ ListClientsQuery.cs            : IQuery<ListResult<ListClientsResult>>
│  │     │  ├─ ListClientsResult.cs           per-item result
│  │     │  └─ ListClientsHandler.cs          : IQueryHandler<...>
│  │     └─ GetClient/
│  │        ├─ GetClientQuery.cs              : IQuery<GetClientResult?>
│  │        ├─ GetClientResult.cs
│  │        └─ GetClientHandler.cs            : IQueryHandler<...>
│  ├─ Fmis.Models/
│  │  ├─ Fmis.Models.csproj
│  │  ├─ Common/
│  │  │  └─ ListResultModel.cs                generic ListResultModel<TModel>
│  │  └─ Clients/
│  │     ├─ CreateClientRequestModel.cs
│  │     └─ ClientResponseModel.cs
│  └─ Fmis.Api/
│     ├─ Fmis.Api.csproj
│     ├─ Program.cs                           DI, auth, ProblemDetails, OpenAPI, endpoints
│     ├─ appsettings.json
│     ├─ Dockerfile
│     └─ Clients/
│        └─ ClientEndpoints.cs                injects ICommandBus/IQueryBus
└─ tests/
   ├─ Fmis.TestSupport/
   │  ├─ Fmis.TestSupport.csproj
   │  ├─ TestDb.cs                            raw-context factory: InMemory + Testcontainers
   │  └─ TestServices.cs                      DI container factory (InMemory) for bus tests
   ├─ Fmis.Core.Tests/
   │  ├─ Fmis.Core.Tests.csproj
   │  ├─ Common/CommandBusTests.cs
   │  └─ Clients/
   │     ├─ CreateClientHandlerTests.cs
   │     ├─ ListClientsHandlerTests.cs
   │     ├─ GetClientHandlerTests.cs
   │     └─ SchemaTests.cs
   └─ Fmis.Api.Tests/
      ├─ Fmis.Api.Tests.csproj
      ├─ TestAuthHandler.cs
      ├─ FmisApiFactory.cs
      ├─ OpenApiTests.cs
      └─ Clients/ClientEndpointsTests.cs
docker-compose.yml                            postgres+postgis, backend
```

---

## Task 1: Solution & project skeleton (with SDK pin)

**Files:**
- Create: `global.json`, `backend/Fmis.sln`, and all `.csproj` files.

- [ ] **Step 1: Pin the SDK to the latest LTS (.NET 10) with `global.json`, then create the solution and source projects**

.NET 10 is the current LTS. Pin it at the **repo root** so every `dotnet` command resolves to it. Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.102",
    "rollForward": "latestFeature"
  }
}
```

`rollForward: latestFeature` keeps the project on .NET 10 (the LTS major) while allowing the newest installed 10.x feature band/patch. (`10.0.102` is the version installed at planning time; adjust to the latest installed 10.x if newer.)

Then from the repo root:

```bash
mkdir -p backend
cd backend
dotnet new sln -n Fmis

dotnet new classlib -n Fmis.Core    -o src/Fmis.Core    -f net10.0
dotnet new classlib -n Fmis.Models  -o src/Fmis.Models  -f net10.0
dotnet new web      -n Fmis.Api     -o src/Fmis.Api     -f net10.0

dotnet new xunit -n Fmis.TestSupport -o tests/Fmis.TestSupport -f net10.0
dotnet new xunit -n Fmis.Core.Tests  -o tests/Fmis.Core.Tests  -f net10.0
dotnet new xunit -n Fmis.Api.Tests   -o tests/Fmis.Api.Tests   -f net10.0
```

- [ ] **Step 2: Remove template throwaway files**

```bash
rm -f src/Fmis.Core/Class1.cs src/Fmis.Models/Class1.cs
rm -f tests/Fmis.TestSupport/UnitTest1.cs
rm -f tests/Fmis.Core.Tests/UnitTest1.cs
rm -f tests/Fmis.Api.Tests/UnitTest1.cs
```

`Fmis.TestSupport` holds shared test utilities and contains **no tests** (we never add `[Fact]`s to it) — it is created from the xunit template only so the test packages are available to helpers.

- [ ] **Step 3: Add all projects to the solution**

```bash
dotnet sln add \
  src/Fmis.Core/Fmis.Core.csproj \
  src/Fmis.Models/Fmis.Models.csproj \
  src/Fmis.Api/Fmis.Api.csproj \
  tests/Fmis.TestSupport/Fmis.TestSupport.csproj \
  tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj \
  tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj
```

- [ ] **Step 4: Wire up project references**

```bash
dotnet add src/Fmis.Api/Fmis.Api.csproj reference src/Fmis.Core/Fmis.Core.csproj src/Fmis.Models/Fmis.Models.csproj
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj reference src/Fmis.Core/Fmis.Core.csproj
dotnet add tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj reference src/Fmis.Core/Fmis.Core.csproj tests/Fmis.TestSupport/Fmis.TestSupport.csproj
dotnet add tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj reference src/Fmis.Api/Fmis.Api.csproj tests/Fmis.TestSupport/Fmis.TestSupport.csproj
```

- [ ] **Step 5: Add NuGet packages**

```bash
# Core: EF Core + Npgsql + design-time + DI abstractions (for the bus + registrations)
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.EntityFrameworkCore
dotnet add src/Fmis.Core/Fmis.Core.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.Extensions.DependencyInjection.Abstractions

# Api: JWT bearer + OpenAPI
dotnet add src/Fmis.Api/Fmis.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Fmis.Api/Fmis.Api.csproj package Microsoft.AspNetCore.OpenApi

# TestSupport: InMemory provider + Testcontainers + DI (for TestServices container)
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj package Microsoft.EntityFrameworkCore.InMemory
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj package Testcontainers.PostgreSql
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj package Microsoft.Extensions.DependencyInjection

# Api.Tests: Mvc.Testing
dotnet add tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 6: Build to verify the skeleton compiles**

Run: `dotnet build backend/Fmis.sln`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add global.json backend/
git commit -m "Add backend solution skeleton and pin SDK via global.json"
```

---

## Task 2: In-house command/query bus

**Files:**
- Create: `backend/src/Fmis.Core/Common/Messaging/ICommand.cs`, `ICommandHandler.cs`, `ICommandBus.cs`, `CommandBus.cs`
- Create: `backend/src/Fmis.Core/Common/Messaging/IQuery.cs`, `IQueryHandler.cs`, `IQueryBus.cs`, `QueryBus.cs`
- Create: `backend/src/Fmis.Core/Common/Messaging/MessagingServiceCollectionExtensions.cs`
- Create: `backend/src/Fmis.Core/Common/ListResult.cs`
- Test: `backend/tests/Fmis.Core.Tests/Common/CommandBusTests.cs`

The bus resolves a handler from DI by the message's runtime type and dispatches. No MediatR. The query side mirrors the command side. `IEventBus` is intentionally omitted until the first domain event exists.

- [ ] **Step 1: Write the failing bus test**

`backend/tests/Fmis.Core.Tests/Common/CommandBusTests.cs`:

```csharp
using Fmis.Core.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests.Common;

public class CommandBusTests
{
    private record PingCommand(string Text) : ICommand<string>;

    private class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> HandleAsync(PingCommand command, CancellationToken cancellationToken)
            => Task.FromResult($"pong:{command.Text}");
    }

    [Fact]
    public async Task Resolves_and_invokes_the_registered_handler_via_DI()
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<ICommandBus>();
        var result = await bus.ExecuteAsync(new PingCommand("hi"), CancellationToken.None);

        Assert.Equal("pong:hi", result);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CommandBusTests"`
Expected: FAIL — messaging types do not exist (compile error).

- [ ] **Step 3: Create the command-side interfaces**

`backend/src/Fmis.Core/Common/Messaging/ICommand.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

/// <summary>A command that produces <typeparamref name="TResult"/>.</summary>
public interface ICommand<TResult>;
```

`backend/src/Fmis.Core/Common/Messaging/ICommandHandler.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
```

`backend/src/Fmis.Core/Common/Messaging/ICommandBus.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

public interface ICommandBus
{
    Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Create the CommandBus**

`backend/src/Fmis.Core/Common/Messaging/CommandBus.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public class CommandBus(IServiceProvider provider) : ICommandBus
{
    public Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        dynamic handler = provider.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)command, cancellationToken);
    }
}
```

- [ ] **Step 5: Create the query-side interfaces and bus (mirror of the command side)**

`backend/src/Fmis.Core/Common/Messaging/IQuery.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

/// <summary>A query that produces <typeparamref name="TResult"/>.</summary>
public interface IQuery<TResult>;
```

`backend/src/Fmis.Core/Common/Messaging/IQueryHandler.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
```

`backend/src/Fmis.Core/Common/Messaging/IQueryBus.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

public interface IQueryBus
{
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);
}
```

`backend/src/Fmis.Core/Common/Messaging/QueryBus.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public class QueryBus(IServiceProvider provider) : IQueryBus
{
    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        dynamic handler = provider.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)query, cancellationToken);
    }
}
```

- [ ] **Step 6: Create the messaging DI extension**

`backend/src/Fmis.Core/Common/Messaging/MessagingServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddScoped<ICommandBus, CommandBus>();
        services.AddScoped<IQueryBus, QueryBus>();
        return services;
    }
}
```

- [ ] **Step 7: Create the generic Core list result**

`backend/src/Fmis.Core/Common/ListResult.cs`:

```csharp
namespace Fmis.Core.Common;

public record ListResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount);
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CommandBusTests"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add backend/src/Fmis.Core/Common/ backend/tests/Fmis.Core.Tests/Common/
git commit -m "Add in-house command/query bus and generic ListResult"
```

---

## Task 3: ClientEntity, FmisDbContext, and Core DI wiring

**Files:**
- Create: `backend/src/Fmis.Core/Clients/ClientEntity.cs`
- Create: `backend/src/Fmis.Core/Clients/ClientConfiguration.cs`
- Create: `backend/src/Fmis.Core/FmisDbContext.cs`
- Create: `backend/src/Fmis.Core/FmisDbContextFactory.cs`
- Create: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`

- [ ] **Step 1: Create the `ClientEntity`**

`backend/src/Fmis.Core/Clients/ClientEntity.cs`:

```csharp
namespace Fmis.Core.Clients;

public class ClientEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}
```

- [ ] **Step 2: Create the entity configuration**

`backend/src/Fmis.Core/Clients/ClientConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fmis.Core.Clients;

public class ClientConfiguration : IEntityTypeConfiguration<ClientEntity>
{
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.Property(c => c.PhoneNumber).HasMaxLength(50);
    }
}
```

- [ ] **Step 3: Create the DbContext**

`backend/src/Fmis.Core/FmisDbContext.cs`:

```csharp
using Fmis.Core.Clients;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core;

public class FmisDbContext(DbContextOptions<FmisDbContext> options) : DbContext(options)
{
    public DbSet<ClientEntity> Clients => Set<ClientEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FmisDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 4: Create the design-time factory**

Connection string is design-time only and matches `docker-compose.yml` (Task 5).

`backend/src/Fmis.Core/FmisDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fmis.Core;

public class FmisDbContextFactory : IDesignTimeDbContextFactory<FmisDbContext>
{
    public FmisDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FmisDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=fmis;Username=fmis;Password=fmis")
            .Options;
        return new FmisDbContext(options);
    }
}
```

- [ ] **Step 5: Create the Core DI extension**

`AddFmisCoreHandlers` is the single composition root for Core services (the bus + every handler). The Api calls `AddFmisCore` (DbContext + handlers); tests call `AddFmisCoreHandlers` with their own DbContext. Handlers are appended here as their slices are built.

`backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`:

```csharp
using Fmis.Core.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddFmisCore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FmisDbContext>(options => options.UseNpgsql(connectionString));
        return services.AddFmisCoreHandlers();
    }

    public static IServiceCollection AddFmisCoreHandlers(this IServiceCollection services)
    {
        services.AddMessaging();
        // Feature handlers are registered here as slices are added (Tasks 6–8).
        return services;
    }
}
```

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Core/Fmis.Core.csproj`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Fmis.Core/
git commit -m "Add ClientEntity, FmisDbContext, and Core DI wiring"
```

---

## Task 4: TestSupport factories (raw context + DI container)

**Files:**
- Create: `backend/tests/Fmis.TestSupport/TestDb.cs`
- Create: `backend/tests/Fmis.TestSupport/TestServices.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs` *(temporary round-trip; replaced in Task 6)*

`TestDb` gives a raw `FmisDbContext` (for persistence/schema tests). `TestServices` gives a real DI container with an InMemory database (for bus-driven slice tests — the same composition the Api uses). Neither uses a mocking framework.

- [ ] **Step 1: Write the raw-context factory**

`backend/tests/Fmis.TestSupport/TestDb.cs`:

```csharp
using Fmis.Core;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Fmis.TestSupport;

/// <summary>
/// Factory for a raw <see cref="FmisDbContext"/>. Use <see cref="InMemory"/> for fast tests,
/// <see cref="ContainerAsync"/> when a real Postgres/PostGIS database is required. No mocking.
/// </summary>
public static class TestDb
{
    public static FmisDbContext InMemory()
    {
        var options = new DbContextOptionsBuilder<FmisDbContext>()
            .UseInMemoryDatabase($"fmis-test-{Guid.NewGuid()}")
            .Options;
        var context = new FmisDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static async Task<ContainerDb> ContainerAsync()
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<FmisDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        var context = new FmisDbContext(options);
        await context.Database.MigrateAsync();
        return new ContainerDb(container, context);
    }
}

public sealed class ContainerDb(PostgreSqlContainer container, FmisDbContext context) : IAsyncDisposable
{
    public FmisDbContext Context { get; } = context;

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await container.DisposeAsync();
    }
}
```

- [ ] **Step 2: Write the DI container factory**

`backend/tests/Fmis.TestSupport/TestServices.cs`:

```csharp
using Fmis.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.TestSupport;

/// <summary>
/// Builds the same Core composition the Api uses, backed by a unique InMemory database.
/// Resolve <c>ICommandBus</c>/<c>IQueryBus</c> from a scope and execute messages — never
/// construct handlers directly.
/// </summary>
public static class TestServices
{
    public static ServiceProvider CreateInMemory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<FmisDbContext>(options =>
            options.UseInMemoryDatabase($"fmis-test-{Guid.NewGuid()}"));
        services.AddFmisCoreHandlers();
        return services.BuildServiceProvider();
    }
}
```

- [ ] **Step 3: Write a temporary round-trip test to confirm the factories wire up**

`backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs`:

```csharp
using Fmis.Core.Clients;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class CreateClientHandlerTests
{
    [Fact]
    public async Task TestDb_InMemory_round_trips_a_client()
    {
        await using var db = TestDb.InMemory();

        db.Clients.Add(new ClientEntity { Id = Guid.NewGuid(), Name = "Acme Farms" });
        await db.SaveChangesAsync();

        Assert.Single(db.Clients);
    }
}
```

> Task 6 replaces this file's contents with the real bus-driven `CreateClient` test.

- [ ] **Step 4: Run the backend test suite**

Run: `dotnet test backend/Fmis.sln`
Expected: PASS (CommandBus test + round-trip test).

- [ ] **Step 5: Commit**

```bash
git add backend/tests/Fmis.TestSupport/ backend/tests/Fmis.Core.Tests/Clients/
git commit -m "Add TestDb and TestServices factories with round-trip test"
```

---

## Task 5: Local Postgres+PostGIS, initial migration, and container schema test

**Files:**
- Create: `docker-compose.yml` (repo root)
- Create: `backend/src/Fmis.Core/Migrations/*` (generated)
- Test: `backend/tests/Fmis.Core.Tests/Clients/SchemaTests.cs`

- [ ] **Step 1: Create `docker-compose.yml` with the database service**

`docker-compose.yml` (repo root):

```yaml
services:
  db:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_DB: fmis
      POSTGRES_USER: fmis
      POSTGRES_PASSWORD: fmis
    ports:
      - "5432:5432"
    volumes:
      - db-data:/var/lib/postgresql/data

volumes:
  db-data:
```

> The backend service is added in Task 15 once the Api runs.

- [ ] **Step 2: Install the EF Core CLI tool (if not already present)**

```bash
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
```

- [ ] **Step 3: Generate the initial migration**

Run from the repo root:

```bash
dotnet ef migrations add InitialCreate \
  --project backend/src/Fmis.Core/Fmis.Core.csproj \
  --output-dir Migrations
```

Expected: a `Migrations/` folder appears in `Fmis.Core` with `*_InitialCreate.cs` creating the `clients` table.

- [ ] **Step 4: Write a failing test that the migration builds the schema on a real container**

`backend/tests/Fmis.Core.Tests/Clients/SchemaTests.cs`:

```csharp
using Fmis.Core.Clients;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class SchemaTests
{
    [Fact]
    public async Task Migrations_create_clients_table_on_real_postgis()
    {
        await using var db = await TestDb.ContainerAsync();

        db.Context.Clients.Add(new ClientEntity { Id = Guid.NewGuid(), Name = "Container Farm" });
        await db.Context.SaveChangesAsync();

        Assert.Single(db.Context.Clients);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes (requires Docker running)**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~SchemaTests"`
Expected: PASS. (First run pulls the `postgis/postgis:16-3.4` image — may take a minute.)

- [ ] **Step 6: Commit**

```bash
git add docker-compose.yml backend/src/Fmis.Core/Migrations/
git commit -m "Add docker-compose db, initial EF migration, container schema test"
```

---

## Task 6: CreateClient slice

**Files:**
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientCommand.cs`
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientResult.cs`
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientHandler.cs`
- Modify: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs` (replace scaffolding)

- [ ] **Step 1: Replace the scaffolding test with the real bus-driven test**

`backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs`:

```csharp
using Fmis.Core;
using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Common.Messaging;
using Fmis.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests.Clients;

public class CreateClientHandlerTests
{
    [Fact]
    public async Task Persists_the_client_and_returns_it_with_a_generated_id()
    {
        await using var provider = TestServices.CreateInMemory();
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();

        var result = await bus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "ops@acme.example", "555-0100"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Acme Farms", result.Name);
        Assert.Equal("ops@acme.example", result.Email);
        Assert.Equal("555-0100", result.PhoneNumber);

        var db = scope.ServiceProvider.GetRequiredService<FmisDbContext>();
        var saved = Assert.Single(db.Clients);
        Assert.Equal(result.Id, saved.Id);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CreateClientHandlerTests"`
Expected: FAIL — `CreateClientCommand` / `CreateClientResult` do not exist (compile error). (`GetRequiredService<ICommandBus>` would also throw at runtime until the handler is registered, but the compile error comes first.)

- [ ] **Step 3: Create the command**

`backend/src/Fmis.Core/Clients/CreateClient/CreateClientCommand.cs`:

```csharp
using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.CreateClient;

public record CreateClientCommand(string Name, string? Email, string? PhoneNumber)
    : ICommand<CreateClientResult>;
```

- [ ] **Step 4: Create the result**

`backend/src/Fmis.Core/Clients/CreateClient/CreateClientResult.cs`:

```csharp
namespace Fmis.Core.Clients.CreateClient;

public record CreateClientResult(Guid Id, string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 5: Create the handler**

`backend/src/Fmis.Core/Clients/CreateClient/CreateClientHandler.cs`:

```csharp
using Fmis.Core.Clients;
using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.CreateClient;

public class CreateClientHandler(FmisDbContext db)
    : ICommandHandler<CreateClientCommand, CreateClientResult>
{
    public async Task<CreateClientResult> HandleAsync(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var client = new ClientEntity
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Email = command.Email,
            PhoneNumber = command.PhoneNumber,
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateClientResult(client.Id, client.Name, client.Email, client.PhoneNumber);
    }
}
```

- [ ] **Step 6: Register the handler**

In `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`, add the using:

```csharp
using Fmis.Core.Clients.CreateClient;
```

and inside `AddFmisCoreHandlers`, after `services.AddMessaging();`:

```csharp
        services.AddScoped<ICommandHandler<CreateClientCommand, CreateClientResult>, CreateClientHandler>();
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CreateClientHandlerTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add CreateClient slice (command, result, handler, registration)"
```

---

## Task 7: ListClients slice

**Files:**
- Create: `backend/src/Fmis.Core/Clients/ListClients/ListClientsResult.cs`
- Create: `backend/src/Fmis.Core/Clients/ListClients/ListClientsQuery.cs`
- Create: `backend/src/Fmis.Core/Clients/ListClients/ListClientsHandler.cs`
- Modify: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/ListClientsHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

`backend/tests/Fmis.Core.Tests/Clients/ListClientsHandlerTests.cs`:

```csharp
using Fmis.Core;
using Fmis.Core.Clients;
using Fmis.Core.Clients.ListClients;
using Fmis.Core.Common.Messaging;
using Fmis.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests.Clients;

public class ListClientsHandlerTests
{
    [Fact]
    public async Task Returns_all_clients_with_total_count()
    {
        await using var provider = TestServices.CreateInMemory();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<FmisDbContext>();
        db.Clients.Add(new ClientEntity { Id = Guid.NewGuid(), Name = "Acme Farms" });
        db.Clients.Add(new ClientEntity { Id = Guid.NewGuid(), Name = "Bedrock Ag" });
        await db.SaveChangesAsync();

        var bus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        var result = await bus.QueryAsync(new ListClientsQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, r => r.Name == "Acme Farms");
        Assert.Contains(result.Items, r => r.Name == "Bedrock Ag");
    }

    [Fact]
    public async Task Returns_empty_with_zero_total_when_there_are_no_clients()
    {
        await using var provider = TestServices.CreateInMemory();
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var result = await bus.QueryAsync(new ListClientsQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~ListClientsHandlerTests"`
Expected: FAIL — `ListClientsQuery` / `ListClientsResult` do not exist.

- [ ] **Step 3: Create the per-item result**

`backend/src/Fmis.Core/Clients/ListClients/ListClientsResult.cs`:

```csharp
namespace Fmis.Core.Clients.ListClients;

public record ListClientsResult(Guid Id, string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 4: Create the query**

`backend/src/Fmis.Core/Clients/ListClients/ListClientsQuery.cs`:

```csharp
using Fmis.Core.Common;
using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.ListClients;

public record ListClientsQuery : IQuery<ListResult<ListClientsResult>>;
```

- [ ] **Step 5: Create the handler**

`backend/src/Fmis.Core/Clients/ListClients/ListClientsHandler.cs`:

```csharp
using Fmis.Core.Common;
using Fmis.Core.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.ListClients;

public class ListClientsHandler(FmisDbContext db)
    : IQueryHandler<ListClientsQuery, ListResult<ListClientsResult>>
{
    public async Task<ListResult<ListClientsResult>> HandleAsync(ListClientsQuery query, CancellationToken cancellationToken)
    {
        var items = await db.Clients
            .OrderBy(c => c.Name)
            .Select(c => new ListClientsResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .ToListAsync(cancellationToken);

        var totalCount = await db.Clients.CountAsync(cancellationToken);

        return new ListResult<ListClientsResult>(items, totalCount);
    }
}
```

- [ ] **Step 6: Register the handler**

In `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`, add:

```csharp
using Fmis.Core.Clients.ListClients;
using Fmis.Core.Common;
```

and inside `AddFmisCoreHandlers`:

```csharp
        services.AddScoped<IQueryHandler<ListClientsQuery, ListResult<ListClientsResult>>, ListClientsHandler>();
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~ListClientsHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 8: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add ListClients slice with ListResult and total count"
```

---

## Task 8: GetClient slice (with not-found)

**Files:**
- Create: `backend/src/Fmis.Core/Clients/GetClient/GetClientResult.cs`
- Create: `backend/src/Fmis.Core/Clients/GetClient/GetClientQuery.cs`
- Create: `backend/src/Fmis.Core/Clients/GetClient/GetClientHandler.cs`
- Modify: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/GetClientHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

`backend/tests/Fmis.Core.Tests/Clients/GetClientHandlerTests.cs`:

```csharp
using Fmis.Core;
using Fmis.Core.Clients;
using Fmis.Core.Clients.GetClient;
using Fmis.Core.Common.Messaging;
using Fmis.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests.Clients;

public class GetClientHandlerTests
{
    [Fact]
    public async Task Returns_the_client_when_it_exists()
    {
        await using var provider = TestServices.CreateInMemory();
        using var scope = provider.CreateScope();

        var id = Guid.NewGuid();
        var db = scope.ServiceProvider.GetRequiredService<FmisDbContext>();
        db.Clients.Add(new ClientEntity { Id = id, Name = "Acme Farms", Email = "ops@acme.example" });
        await db.SaveChangesAsync();

        var bus = scope.ServiceProvider.GetRequiredService<IQueryBus>();
        var result = await bus.QueryAsync(new GetClientQuery(id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal("Acme Farms", result.Name);
    }

    [Fact]
    public async Task Returns_null_when_the_client_does_not_exist()
    {
        await using var provider = TestServices.CreateInMemory();
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        var result = await bus.QueryAsync(new GetClientQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~GetClientHandlerTests"`
Expected: FAIL — `GetClientQuery` / `GetClientResult` do not exist.

- [ ] **Step 3: Create the result**

`backend/src/Fmis.Core/Clients/GetClient/GetClientResult.cs`:

```csharp
namespace Fmis.Core.Clients.GetClient;

public record GetClientResult(Guid Id, string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 4: Create the query**

Returning `GetClientResult?` makes "not found" an expected `null` the Api maps to 404.

`backend/src/Fmis.Core/Clients/GetClient/GetClientQuery.cs`:

```csharp
using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.GetClient;

public record GetClientQuery(Guid Id) : IQuery<GetClientResult?>;
```

- [ ] **Step 5: Create the handler**

`backend/src/Fmis.Core/Clients/GetClient/GetClientHandler.cs`:

```csharp
using Fmis.Core.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.GetClient;

public class GetClientHandler(FmisDbContext db)
    : IQueryHandler<GetClientQuery, GetClientResult?>
{
    public async Task<GetClientResult?> HandleAsync(GetClientQuery query, CancellationToken cancellationToken)
    {
        return await db.Clients
            .Where(c => c.Id == query.Id)
            .Select(c => new GetClientResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

- [ ] **Step 6: Register the handler**

In `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`, add:

```csharp
using Fmis.Core.Clients.GetClient;
```

and inside `AddFmisCoreHandlers`:

```csharp
        services.AddScoped<IQueryHandler<GetClientQuery, GetClientResult?>, GetClientHandler>();
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~GetClientHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 8: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add GetClient slice with not-found handling"
```

---

## Task 9: External API models (DTOs)

**Files:**
- Create: `backend/src/Fmis.Models/Clients/CreateClientRequestModel.cs`
- Create: `backend/src/Fmis.Models/Clients/ClientResponseModel.cs`
- Create: `backend/src/Fmis.Models/Common/ListResultModel.cs`

These are the external contract the Api exposes (mirrored by frontend Zod in Plan 2), deliberately separate from Core's result types.

- [ ] **Step 1: Create the request model**

`backend/src/Fmis.Models/Clients/CreateClientRequestModel.cs`:

```csharp
namespace Fmis.Models.Clients;

public record CreateClientRequestModel(string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 2: Create the response model**

`backend/src/Fmis.Models/Clients/ClientResponseModel.cs`:

```csharp
namespace Fmis.Models.Clients;

public record ClientResponseModel(Guid Id, string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 3: Create the generic list result model**

`backend/src/Fmis.Models/Common/ListResultModel.cs`:

```csharp
namespace Fmis.Models.Common;

public record ListResultModel<TModel>(IReadOnlyList<TModel> Items, int TotalCount);
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Models/Fmis.Models.csproj`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Fmis.Models/
git commit -m "Add Client request/response models and generic ListResultModel"
```

---

## Task 10: Api host — DI, ProblemDetails, OpenAPI, authentication

**Files:**
- Modify: `backend/src/Fmis.Api/Program.cs` (replace template)
- Create/replace: `backend/src/Fmis.Api/appsettings.json`

- [ ] **Step 1: Replace `Program.cs`**

`backend/src/Fmis.Api/Program.cs`:

```csharp
using Fmis.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFmisCore(builder.Configuration.GetConnectionString("Fmis")
    ?? throw new InvalidOperationException("Missing connection string 'Fmis'."));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Authentication only — verify identity via Auth0 JWT. No authorization policies.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth0:Authority"];
        options.Audience = builder.Configuration["Auth0:Audience"];
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

// Client endpoints are mapped here in Task 11:
// app.MapClientEndpoints();

app.Run();

// Exposed so Fmis.Api.Tests' WebApplicationFactory<Program> can reference it.
public partial class Program;
```

- [ ] **Step 2: Replace `appsettings.json`**

`backend/src/Fmis.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Fmis": "Host=localhost;Port=5432;Database=fmis;Username=fmis;Password=fmis"
  },
  "Auth0": {
    "Authority": "",
    "Audience": ""
  }
}
```

> `Auth0` values are blank locally; the real values come from the `auth` Pulumi stack (Plan 3). With no token presented, protected endpoints return 401 without contacting Auth0.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Api/Fmis.Api.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Fmis.Api/Program.cs backend/src/Fmis.Api/appsettings.json
git commit -m "Configure Api host: Core DI, ProblemDetails, OpenAPI, JWT auth"
```

---

## Task 11: Client endpoints (HTTP ↔ Models ↔ bus)

**Files:**
- Create: `backend/src/Fmis.Api/Clients/ClientEndpoints.cs`
- Modify: `backend/src/Fmis.Api/Program.cs` (map endpoints)

Endpoints inject `ICommandBus`/`IQueryBus` (never handlers), translate `Models` ↔ Core messages, and map list results into `ListResultModel<ClientResponseModel>`. All endpoints require an authenticated user. Validation happens here (before Core). Behavior is covered by the integration tests in Task 13.

- [ ] **Step 1: Create the endpoints**

`backend/src/Fmis.Api/Clients/ClientEndpoints.cs`:

```csharp
using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.GetClient;
using Fmis.Core.Clients.ListClients;
using Fmis.Core.Common.Messaging;
using Fmis.Models.Clients;
using Fmis.Models.Common;

namespace Fmis.Api.Clients;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder routes)
    {
        // RequireAuthorization() with no policy enforces authentication only (an authenticated user).
        var group = routes.MapGroup("/clients").RequireAuthorization();

        group.MapPost("/", CreateClient);
        group.MapGet("/", ListClients);
        group.MapGet("/{id:guid}", GetClient);

        return routes;
    }

    private static async Task<IResult> CreateClient(
        CreateClientRequestModel request,
        ICommandBus bus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."],
            });
        }

        var result = await bus.ExecuteAsync(
            new CreateClientCommand(request.Name, request.Email, request.PhoneNumber),
            cancellationToken);

        var model = new ClientResponseModel(result.Id, result.Name, result.Email, result.PhoneNumber);
        return Results.Created($"/clients/{model.Id}", model);
    }

    private static async Task<IResult> ListClients(
        IQueryBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.QueryAsync(new ListClientsQuery(), cancellationToken);

        var items = result.Items
            .Select(i => new ClientResponseModel(i.Id, i.Name, i.Email, i.PhoneNumber))
            .ToList();

        return Results.Ok(new ListResultModel<ClientResponseModel>(items, result.TotalCount));
    }

    private static async Task<IResult> GetClient(
        Guid id,
        IQueryBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.QueryAsync(new GetClientQuery(id), cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(new ClientResponseModel(result.Id, result.Name, result.Email, result.PhoneNumber));
    }
}
```

- [ ] **Step 2: Map the endpoints in `Program.cs`**

In `backend/src/Fmis.Api/Program.cs`, add the using at the top:

```csharp
using Fmis.Api.Clients;
```

and replace

```csharp
// Client endpoints are mapped here in Task 11:
// app.MapClientEndpoints();
```

with:

```csharp
app.MapClientEndpoints();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Api/Fmis.Api.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Fmis.Api/
git commit -m "Add Client endpoints injecting the command/query bus"
```

---

## Task 12: Test authentication scheme + WebApplicationFactory

**Files:**
- Create: `backend/tests/Fmis.Api.Tests/TestAuthHandler.cs`
- Create: `backend/tests/Fmis.Api.Tests/FmisApiFactory.cs`

The Api tests must not call real Auth0. The factory swaps in a hand-written "Test" authentication scheme (no mocking framework) that authenticates only when an `Authorization` header is present — so tests control authenticated vs. unauthenticated by setting or omitting it. The factory also points the DbContext at a fresh InMemory database.

- [ ] **Step 1: Create the test auth handler**

`backend/tests/Fmis.Api.Tests/TestAuthHandler.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fmis.Api.Tests;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

- [ ] **Step 2: Create the WebApplicationFactory**

`backend/tests/Fmis.Api.Tests/FmisApiFactory.cs`:

```csharp
using Fmis.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fmis.Api.Tests;

public class FmisApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace the real Npgsql DbContext with a per-factory InMemory database.
            services.RemoveAll(typeof(DbContextOptions<FmisDbContext>));
            services.AddDbContext<FmisDbContext>(options =>
                options.UseInMemoryDatabase($"fmis-api-tests-{Guid.NewGuid()}"));

            // Replace JWT bearer with the test scheme as the default for authenticate + challenge.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/tests/Fmis.Api.Tests/TestAuthHandler.cs backend/tests/Fmis.Api.Tests/FmisApiFactory.cs
git commit -m "Add test auth scheme and WebApplicationFactory for Api tests"
```

---

## Task 13: Client endpoint integration tests

**Files:**
- Create: `backend/tests/Fmis.Api.Tests/Clients/ClientEndpointsTests.cs`

These exercise the full HTTP pipeline (routing, auth, validation, bus, EF) via `Mvc.Testing` — authenticated success, 404, 400 validation, and the 401 unauthenticated path.

- [ ] **Step 1: Write the failing tests**

`backend/tests/Fmis.Api.Tests/Clients/ClientEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fmis.Models.Clients;
using Fmis.Models.Common;

namespace Fmis.Api.Tests.Clients;

public class ClientEndpointsTests(FmisApiFactory factory) : IClassFixture<FmisApiFactory>
{
    private HttpClient AuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "user");
        return client;
    }

    [Fact]
    public async Task Create_then_get_returns_the_created_client()
    {
        var client = AuthenticatedClient();

        var create = await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("Acme Farms", "ops@acme.example", "555-0100"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<ClientResponseModel>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);

        var get = await client.GetAsync($"/clients/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var fetched = await get.Content.ReadFromJsonAsync<ClientResponseModel>();
        Assert.Equal("Acme Farms", fetched!.Name);
    }

    [Fact]
    public async Task List_returns_created_clients_with_total_count()
    {
        var client = AuthenticatedClient();
        await client.PostAsJsonAsync("/clients", new CreateClientRequestModel("Bedrock Ag", null, null));

        var list = await client.GetFromJsonAsync<ListResultModel<ClientResponseModel>>("/clients");

        Assert.NotNull(list);
        Assert.Contains(list!.Items, c => c.Name == "Bedrock Ag");
        Assert.True(list.TotalCount >= 1);
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        var client = AuthenticatedClient();

        var response = await client.GetAsync($"/clients/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_blank_name_returns_400()
    {
        var client = AuthenticatedClient();

        var response = await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_authentication_returns_401()
    {
        var client = factory.CreateClient(); // no Authorization header

        var response = await client.GetAsync("/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj`
Expected: PASS (5 tests). If the 401 test returns 404, confirm `RequireAuthorization()` is on the group and `UseAuthentication()/UseAuthorization()` are in `Program.cs`.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/Fmis.Api.Tests/Clients/
git commit -m "Add Client endpoint integration tests (success, 404, 400, 401)"
```

---

## Task 14: OpenAPI document smoke test

**Files:**
- Create: `backend/tests/Fmis.Api.Tests/OpenApiTests.cs`

The OpenAPI document is what Plan 2's Zod↔OpenAPI contract test verifies against. This confirms it is served.

- [ ] **Step 1: Write the failing test**

`backend/tests/Fmis.Api.Tests/OpenApiTests.cs`:

```csharp
using System.Net;

namespace Fmis.Api.Tests;

public class OpenApiTests(FmisApiFactory factory) : IClassFixture<FmisApiFactory>
{
    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/clients", body);
    }
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj --filter "FullyQualifiedName~OpenApiTests"`
Expected: PASS. (If `/clients` is absent, confirm `MapOpenApi()` runs and endpoints are mapped before `app.Run()`.)

- [ ] **Step 3: Run the entire backend suite**

Run: `dotnet test backend/Fmis.sln`
Expected: ALL PASS (bus test, Core handler tests, container schema test, Api integration incl. 401, OpenAPI test).

- [ ] **Step 4: Commit**

```bash
git add backend/tests/Fmis.Api.Tests/OpenApiTests.cs
git commit -m "Add OpenAPI document smoke test"
```

---

## Task 15: Backend Dockerfile and docker-compose service

**Files:**
- Create: `backend/src/Fmis.Api/Dockerfile`
- Create: `backend/.dockerignore`
- Modify: `backend/src/Fmis.Api/Program.cs` (startup migration)
- Modify: `docker-compose.yml` (add `backend` service)

- [ ] **Step 1: Create the Dockerfile**

`backend/src/Fmis.Api/Dockerfile`:

```dockerfile
# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Fmis.sln
RUN dotnet publish src/Fmis.Api/Fmis.Api.csproj -c Release -o /app/publish

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fmis.Api.dll"]
```

- [ ] **Step 2: Create `.dockerignore`**

`backend/.dockerignore`:

```
**/bin
**/obj
```

- [ ] **Step 3: Apply migrations on startup**

So the containerized API creates its schema on boot. First add `using Microsoft.EntityFrameworkCore;` to the top of `backend/src/Fmis.Api/Program.cs` (needed for the `Migrate()` extension). Then, immediately after `var app = builder.Build();`, add:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Fmis.Core.FmisDbContext>();
    db.Database.Migrate();
}
```

- [ ] **Step 4: Add the backend service to `docker-compose.yml`**

Replace `docker-compose.yml` (repo root) with:

```yaml
services:
  db:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_DB: fmis
      POSTGRES_USER: fmis
      POSTGRES_PASSWORD: fmis
    ports:
      - "5432:5432"
    volumes:
      - db-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U fmis -d fmis"]
      interval: 5s
      timeout: 5s
      retries: 10

  backend:
    build:
      context: ./backend
      dockerfile: src/Fmis.Api/Dockerfile
    environment:
      ConnectionStrings__Fmis: "Host=db;Port=5432;Database=fmis;Username=fmis;Password=fmis"
    ports:
      - "8080:8080"
    depends_on:
      db:
        condition: service_healthy

volumes:
  db-data:
```

- [ ] **Step 5: Build and run the stack**

```bash
docker compose up --build -d
```

Expected: both `db` and `backend` start; `docker compose ps` shows them healthy/running.

- [ ] **Step 6: Verify the API enforces auth and serves OpenAPI**

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/clients
```
Expected: `401`.

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/openapi/v1.json
```
Expected: `200`.

- [ ] **Step 7: Tear down**

```bash
docker compose down
```

- [ ] **Step 8: Commit**

```bash
git add backend/src/Fmis.Api/Dockerfile backend/.dockerignore docker-compose.yml backend/src/Fmis.Api/Program.cs
git commit -m "Add backend Dockerfile and docker-compose service with startup migration"
```

---

## Done criteria

- `dotnet test backend/Fmis.sln` passes all tests (bus dispatch, Core slices via the bus, real-container schema test, Api integration incl. 401, OpenAPI smoke).
- `docker compose up --build` runs db + backend; unauthenticated `/clients` returns 401; `/openapi/v1.json` returns 200.
- The Client slice exists end-to-end through Api → Models → bus → Core handler → EF Core → Postgres, with authentication enforced and no authorization rules.
- Patterns established for Plan 2/3 to copy: vertical slice layout, in-house command/query bus, handler-via-DI (never constructed), `*Entity`/`*Model`/`*Result` naming, generic `ListResult`/`ListResultModel`, Core DI composition root, TestDb + TestServices, no-mocks testing, test auth scheme.
```
