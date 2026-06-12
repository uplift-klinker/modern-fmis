# Backend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working, authenticated ASP.NET Core API for managing `Client` records end-to-end (Create / List / Get-by-id), establishing the modular-monolith + vertical-slice + in-house-bus + TDD patterns that every later phase copies.

**Architecture:** Modular monolith with vertical slices. `Fmis.Api` (HTTP) → `Fmis.Core` (vertical slices + EF Core) and `Fmis.Models` (external DTOs). Core knows nothing about HTTP or external DTOs; the Api translates between them. Slices are invoked through an in-house command/query **bus** resolved from DI — handlers are auto-discovered by reflection and never constructed directly. The command bus **validates each command** (FluentValidation) before dispatch. Persistence (EF Core / Npgsql) lives inside Core; handlers depend on `FmisDbContext` directly (no repository abstraction, no mocking frameworks). Authentication only (Auth0 JWT bearer) — no authorization rules.

**Tech Stack:** .NET 10 — the current LTS (`net10.0`), pinned via a repo-root `global.json` — ASP.NET Core MVC controllers, EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL`, PostgreSQL+PostGIS (via Docker), FluentValidation, xUnit, `Microsoft.AspNetCore.Mvc.Testing`, EF Core InMemory provider, built-in `Microsoft.AspNetCore.OpenApi`.

**Conventions (from `docs/conventions/`):** New commits only — never amend or force-push. `*Entity` for EF entities, `*Model` for external DTOs, per-operation `*Result` types, generic `ListResult<T>` / `ListResultModel<T>`. In-house command/query bus — no MediatR, reflection-based handler discovery, no direct handler construction. No mocking frameworks. Tests exercise slices through the bus resolved from a real DI container. One test project per production project, plus a no-tests `Fmis.TestSupport`.

**Out of scope (later plans):** React frontend, Zod, Auth0 login UI, Playwright (Plan 2). Pulumi stacks, CI/CD, Azure deploy (Plan 3). PostGIS/spatial columns, the Ingestion project, `IEventBus`/domain events, and a Testcontainers-backed test database (added with the first test needing real Postgres — the PostGIS/Field phase). The DB image is PostGIS-capable but no spatial features are used here.

---

## File Structure

```
global.json                                   pins .NET SDK to the latest LTS (10.x)
.config/dotnet-tools.json                     local dotnet tools (dotnet-ef)
backend/
├─ Fmis.slnx
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
│  │  │     └─ MessagingServiceCollectionExtensions.cs   (reflection discovery)
│  │  ├─ Migrations/                          EF migrations (generated)
│  │  └─ Clients/
│  │     ├─ ClientEntity.cs                   entity
│  │     ├─ ClientConfiguration.cs            IEntityTypeConfiguration<ClientEntity>
│  │     ├─ ClientResult.cs                   shared read result (get-by-id + list)
│  │     ├─ CreateClient/
│  │     │  ├─ CreateClientCommand.cs         : ICommand<CreateClientResult>
│  │     │  ├─ CreateClientResult.cs
│  │     │  ├─ CreateClientCommandValidator.cs : AbstractValidator<CreateClientCommand>
│  │     │  └─ CreateClientHandler.cs         : ICommandHandler<...>
│  │     ├─ ListClients/
│  │     │  ├─ ListClientsQuery.cs            : IQuery<ListResult<ClientResult>>
│  │     │  └─ ListClientsHandler.cs          : IQueryHandler<...>
│  │     └─ GetClient/
│  │        ├─ GetClientQuery.cs              : IQuery<ClientResult?>
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
│     ├─ Program.cs                           thin: AddApiServices → MigrateDatabase/UseApiPipeline/MapApiEndpoints
│     ├─ appsettings.json
│     ├─ Dockerfile
│     ├─ Configuration/
│     │  ├─ ApiServiceCollectionExtensions.cs  AddApiServices (+ error handling, docs, auth)
│     │  └─ ApiApplicationBuilderExtensions.cs UseApiPipeline / MapApiEndpoints / MigrateDatabase
│     ├─ Common/
│     │  └─ ValidationExceptionHandler.cs     ValidationException → 400
│     └─ Clients/
│        └─ ClientsController.cs              [ApiController]; injects ICommandBus/IQueryBus
└─ tests/
   ├─ Fmis.TestSupport/
   │  ├─ Fmis.TestSupport.csproj
   │  ├─ TestServices.cs                      DI container factory (InMemory) for bus tests
   │  └─ InMemoryCoreTestBase.cs                       base class: owns scope, exposes buses/Db, disposes
   ├─ Fmis.Core.Tests/
   │  ├─ Fmis.Core.Tests.csproj
   │  ├─ Common/CommandBusTests.cs
   │  └─ Clients/
   │     ├─ CreateClientHandlerTests.cs
   │     ├─ ListClientsHandlerTests.cs
   │     └─ GetClientHandlerTests.cs
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
- Create: `global.json`, `backend/Fmis.slnx`, and all `.csproj` files.

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
# Core: EF Core + Npgsql + design-time + DI abstractions + FluentValidation
# Pin the EF Core packages to the version the Npgsql provider targets (Npgsql 10.0.2 -> EF Core 10.0.4)
# so all Microsoft.EntityFrameworkCore.* resolve to the same version (no Relational conflict / MSB3277).
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.EntityFrameworkCore --version 10.0.4
dotnet add src/Fmis.Core/Fmis.Core.csproj package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.2
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.EntityFrameworkCore.Design --version 10.0.4
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/Fmis.Core/Fmis.Core.csproj package FluentValidation
dotnet add src/Fmis.Core/Fmis.Core.csproj package FluentValidation.DependencyInjectionExtensions

# Api: JWT bearer + OpenAPI
dotnet add src/Fmis.Api/Fmis.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Fmis.Api/Fmis.Api.csproj package Microsoft.AspNetCore.OpenApi

# TestSupport: InMemory provider + DI (for the TestServices container)
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj package Microsoft.EntityFrameworkCore.InMemory --version 10.0.4
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj package Microsoft.Extensions.DependencyInjection

# Api.Tests: Mvc.Testing
dotnet add tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 6: Build to verify the skeleton compiles**

Run: `dotnet build backend/Fmis.slnx`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add global.json backend/
git commit -m "Add backend solution skeleton and pin SDK via global.json"
```

---

## Task 2: In-house command/query bus (with discovery + validation)

**Files:**
- Create: `backend/src/Fmis.Core/Common/Messaging/ICommand.cs`, `ICommandHandler.cs`, `ICommandBus.cs`, `CommandBus.cs`
- Create: `backend/src/Fmis.Core/Common/Messaging/IQuery.cs`, `IQueryHandler.cs`, `IQueryBus.cs`, `QueryBus.cs`
- Create: `backend/src/Fmis.Core/Common/Messaging/MessagingServiceCollectionExtensions.cs`
- Create: `backend/src/Fmis.Core/Common/ListResult.cs`
- Test: `backend/tests/Fmis.Core.Tests/Common/CommandBusTests.cs`

The bus resolves a handler from DI by the message's runtime type and dispatches. `AddMessaging(params Assembly[])` registers the buses and **discovers/registers every `ICommandHandler<,>` and `IQueryHandler<,>` in the given assemblies by reflection** — no per-handler registration. The command bus also resolves an optional `IValidator<TCommand>` and validates before dispatch. `IEventBus` is intentionally omitted until the first domain event exists.

- [ ] **Step 1: Write the failing bus tests (dispatch + validation)**

`backend/tests/Fmis.Core.Tests/Common/CommandBusTests.cs`:

```csharp
using FluentValidation;
using Fmis.Core.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests.Common;

public class CommandBusTests
{
    public record PingCommand(string Text) : ICommand<string>;

    public class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> HandleAsync(PingCommand command, CancellationToken cancellationToken)
            => Task.FromResult($"pong:{command.Text}");
    }

    public class PingCommandValidator : AbstractValidator<PingCommand>
    {
        public PingCommandValidator() => RuleFor(c => c.Text).NotEmpty();
    }

    [Fact]
    public async Task Resolves_and_invokes_the_registered_handler_via_DI()
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<ICommandBus>();
        var result = await bus.ExecuteAsync(new PingCommand("hi"));

        Assert.Equal("pong:hi", result);
    }

    [Fact]
    public async Task Validates_the_command_and_throws_when_invalid()
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        services.AddScoped<IValidator<PingCommand>, PingCommandValidator>();
        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<ICommandBus>();

        await Assert.ThrowsAsync<ValidationException>(
            () => bus.ExecuteAsync(new PingCommand("")));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CommandBusTests"`
Expected: FAIL — messaging types do not exist (compile error).

- [ ] **Step 3: Create the command-side interfaces**

`backend/src/Fmis.Core/Common/Messaging/ICommand.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

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
    Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Create the CommandBus (with validation)**

`backend/src/Fmis.Core/Common/Messaging/CommandBus.cs`:

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public class CommandBus(IServiceProvider provider) : ICommandBus
{
    public async Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(command, cancellationToken);

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        dynamic handler = provider.GetRequiredService(handlerType);
        return await handler.HandleAsync((dynamic)command, cancellationToken);
    }

    private async Task ValidateAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(command.GetType());
        if (provider.GetService(validatorType) is IValidator validator)
        {
            var context = new ValidationContext<object>(command);
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }
    }
}
```

- [ ] **Step 5: Create the query-side interfaces and bus (mirror of the command side, no validation)**

`backend/src/Fmis.Core/Common/Messaging/IQuery.cs`:

```csharp
namespace Fmis.Core.Common.Messaging;

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
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
```

`backend/src/Fmis.Core/Common/Messaging/QueryBus.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public class QueryBus(IServiceProvider provider) : IQueryBus
{
    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        dynamic handler = provider.GetRequiredService(handlerType);
        return await handler.HandleAsync((dynamic)query, cancellationToken);
    }
}
```

- [ ] **Step 6: Create the messaging DI extension with reflection discovery**

`backend/src/Fmis.Core/Common/Messaging/MessagingServiceCollectionExtensions.cs`:

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<ICommandBus, CommandBus>();
        services.AddScoped<IQueryBus, QueryBus>();

        foreach (var assembly in assemblies.Distinct())
        {
            RegisterImplementations(services, assembly, typeof(ICommandHandler<,>));
            RegisterImplementations(services, assembly, typeof(IQueryHandler<,>));
        }

        return services;
    }

    private static void RegisterImplementations(IServiceCollection services, Assembly assembly, Type openHandlerInterface)
    {
        var types = assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false });
        foreach (var type in types)
        {
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerInterface);

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddScoped(handlerInterface, type);
            }
        }
    }
}
```

> Called with no assemblies (as in the bus unit test), it registers only the buses, so tests can register their own handlers explicitly. Called with the Core assembly (Task 3), it discovers all real handlers.

- [ ] **Step 7: Create the generic Core list result**

`backend/src/Fmis.Core/Common/ListResult.cs`:

```csharp
namespace Fmis.Core.Common;

public record ListResult<TItem>(IReadOnlyList<TItem> Items, int TotalCount);
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CommandBusTests"`
Expected: PASS (2 tests).

- [ ] **Step 9: Commit**

```bash
git add backend/src/Fmis.Core/Common/ backend/tests/Fmis.Core.Tests/Common/
git commit -m "Add in-house command/query bus with discovery and command validation"
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

- [ ] **Step 5: Create the Core DI extension (discovery + validators)**

`AddFmisCoreHandlers` is the single composition root for Core services. It discovers handlers and validators by scanning the Core assembly — handlers are **never** registered one-by-one. The Api calls `AddFmisCore` (DbContext + handlers); tests call `AddFmisCoreHandlers` with their own DbContext.

`backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`:

```csharp
using Fmis.Core.Common.Messaging;
using FluentValidation;
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
        var coreAssembly = typeof(CoreServiceCollectionExtensions).Assembly;
        services.AddMessaging(coreAssembly);
        services.AddValidatorsFromAssembly(coreAssembly);
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
git commit -m "Add ClientEntity, FmisDbContext, and Core DI wiring with discovery"
```

---

## Task 4: TestServices factory and InMemoryCoreTestBase

**Files:**
- Create: `backend/tests/Fmis.TestSupport/TestServices.cs`
- Create: `backend/tests/Fmis.TestSupport/InMemoryCoreTestBase.cs`

`TestServices` builds the same Core composition the Api uses (buses, discovered handlers, validators), backed by a unique InMemory database. `InMemoryCoreTestBase` is the base class every slice test inherits: it creates the provider + scope once in its constructor, exposes `CommandBus`/`QueryBus`/`Db`, and disposes everything via `IDisposable` (xUnit calls `Dispose` after each test). This removes the per-test scope boilerplate. The name advertises the backing store; a sibling `ContainerCoreTestBase` (Testcontainers-backed) is added later when a test first needs real Postgres — likely sharing a common abstract base with this one at that point.

- [ ] **Step 1: Write the DI container factory**

`backend/tests/Fmis.TestSupport/TestServices.cs`:

```csharp
using Fmis.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.TestSupport;

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

- [ ] **Step 2: Write the base test class**

`backend/tests/Fmis.TestSupport/InMemoryCoreTestBase.cs`:

```csharp
using Fmis.Core;
using Fmis.Core.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.TestSupport;

public abstract class InMemoryCoreTestBase : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    protected InMemoryCoreTestBase()
    {
        _provider = TestServices.CreateInMemory();
        _scope = _provider.CreateScope();
    }

    protected IServiceProvider Services => _scope.ServiceProvider;
    protected ICommandBus CommandBus => Services.GetRequiredService<ICommandBus>();
    protected IQueryBus QueryBus => Services.GetRequiredService<IQueryBus>();
    protected FmisDbContext Db => Services.GetRequiredService<FmisDbContext>();

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/tests/Fmis.TestSupport/Fmis.TestSupport.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/tests/Fmis.TestSupport/
git commit -m "Add TestServices factory and InMemoryCoreTestBase for slice tests"
```

---

## Task 5: Local Postgres+PostGIS and initial migration

**Files:**
- Create: `docker-compose.yml` (repo root)
- Create: `backend/src/Fmis.Core/Migrations/*` (generated)

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

- [ ] **Step 2: Install the EF Core CLI as a LOCAL tool**

Use a repo-local tool manifest (`.config/dotnet-tools.json`) — never a global tool — so the EF CLI version is pinned and reproducible. Run from the repo root:

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

This creates `.config/dotnet-tools.json` at the repo root. The migration command below then resolves `dotnet ef` from this manifest.

- [ ] **Step 3: Generate the initial migration**

Run from the repo root:

```bash
dotnet ef migrations add InitialCreate \
  --project backend/src/Fmis.Core/Fmis.Core.csproj \
  --output-dir Migrations
```

Expected: a `Migrations/` folder appears in `Fmis.Core` with `*_InitialCreate.cs` creating the `clients` table.

- [ ] **Step 4: Build to verify the generated migration compiles**

Run: `dotnet build backend/src/Fmis.Core/Fmis.Core.csproj`
Expected: `Build succeeded`. (The migration is applied for real at API startup in Task 15.)

- [ ] **Step 5: Commit**

```bash
git add .config/dotnet-tools.json docker-compose.yml backend/src/Fmis.Core/Migrations/
git commit -m "Add local dotnet-ef tool, docker-compose db service, and initial EF migration"
```

---

## Task 6: CreateClient slice (with validation)

**Files:**
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientCommand.cs`
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientResult.cs`
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientCommandValidator.cs`
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientHandler.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs`

Handlers and validators are auto-discovered (Task 3) — no registration step. A client requires a name **and** at least one contact method (email or phone).

- [ ] **Step 1: Write the failing tests (success + both validation rules)**

`backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs`:

```csharp
using FluentValidation;
using Fmis.Core.Clients.CreateClient;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class CreateClientHandlerTests : InMemoryCoreTestBase
{
    [Fact]
    public async Task Persists_the_client_and_returns_it_with_a_generated_id()
    {
        var result = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "ops@acme.example", "555-0100"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Acme Farms", result.Name);
        Assert.Equal("ops@acme.example", result.Email);
        Assert.Equal("555-0100", result.PhoneNumber);

        var saved = Assert.Single(Db.Clients);
        Assert.Equal(result.Id, saved.Id);
    }

    [Fact]
    public async Task Accepts_a_client_with_only_a_phone_number()
    {
        var result = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", null, "555-0100"));

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Accepts_a_client_with_only_an_email()
    {
        var result = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "ops@acme.example", null));

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Rejects_a_blank_name()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CommandBus.ExecuteAsync(
            new CreateClientCommand("", "ops@acme.example", null)));
    }

    [Fact]
    public async Task Rejects_a_client_with_no_email_or_phone()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", null, null)));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CreateClientHandlerTests"`
Expected: FAIL — `CreateClientCommand` / `CreateClientResult` do not exist (compile error).

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

- [ ] **Step 5: Create the validator**

`backend/src/Fmis.Core/Clients/CreateClient/CreateClientCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Fmis.Core.Clients.CreateClient;

public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Name is required.");

        RuleFor(c => c)
            .Must(HasContactMethod)
            .WithName("contact")
            .WithMessage("Either an email or a phone number is required.");
    }

    private static bool HasContactMethod(CreateClientCommand command)
        => !string.IsNullOrWhiteSpace(command.Email) || !string.IsNullOrWhiteSpace(command.PhoneNumber);
}
```

- [ ] **Step 6: Create the handler**

`backend/src/Fmis.Core/Clients/CreateClient/CreateClientHandler.cs`:

```csharp
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

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CreateClientHandlerTests"`
Expected: PASS (5 tests). The handler and validator are auto-discovered by `AddFmisCoreHandlers`.

- [ ] **Step 8: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add CreateClient slice with command validation"
```

---

## Task 7: ListClients slice

**Files:**
- Create: `backend/src/Fmis.Core/Clients/ClientResult.cs` (shared read result — used here and by GetClient)
- Create: `backend/src/Fmis.Core/Clients/ListClients/ListClientsQuery.cs`
- Create: `backend/src/Fmis.Core/Clients/ListClients/ListClientsHandler.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/ListClientsHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

Tests seed data by creating clients **through the command bus** (the real write flow), then query through the bus.

`backend/tests/Fmis.Core.Tests/Clients/ListClientsHandlerTests.cs`:

```csharp
using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.ListClients;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class ListClientsHandlerTests : InMemoryCoreTestBase
{
    [Fact]
    public async Task Returns_all_clients_with_total_count()
    {
        await CommandBus.ExecuteAsync(new CreateClientCommand("Acme Farms", "ops@acme.example", null));
        await CommandBus.ExecuteAsync(new CreateClientCommand("Bedrock Ag", "info@bedrock.example", null));

        var result = await QueryBus.QueryAsync(new ListClientsQuery());

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, r => r.Name == "Acme Farms");
        Assert.Contains(result.Items, r => r.Name == "Bedrock Ag");
    }

    [Fact]
    public async Task Returns_empty_with_zero_total_when_there_are_no_clients()
    {
        var result = await QueryBus.QueryAsync(new ListClientsQuery());

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~ListClientsHandlerTests"`
Expected: FAIL — `ListClientsQuery` / `ClientResult` do not exist.

- [ ] **Step 3: Create the shared read result**

`ClientResult` is the canonical client read shape, shared by get-by-id (singular) and list (`ListResult<ClientResult>`). It lives at the `Clients` area level, not inside a feature folder.

`backend/src/Fmis.Core/Clients/ClientResult.cs`:

```csharp
namespace Fmis.Core.Clients;

public record ClientResult(Guid Id, string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 4: Create the query**

`backend/src/Fmis.Core/Clients/ListClients/ListClientsQuery.cs`:

```csharp
using Fmis.Core.Common;
using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.ListClients;

public record ListClientsQuery : IQuery<ListResult<ClientResult>>;
```

- [ ] **Step 5: Create the handler**

`backend/src/Fmis.Core/Clients/ListClients/ListClientsHandler.cs`:

```csharp
using Fmis.Core.Common;
using Fmis.Core.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.ListClients;

public class ListClientsHandler(FmisDbContext db)
    : IQueryHandler<ListClientsQuery, ListResult<ClientResult>>
{
    public async Task<ListResult<ClientResult>> HandleAsync(ListClientsQuery query, CancellationToken cancellationToken)
    {
        var items = await db.Clients
            .OrderBy(c => c.Name)
            .Select(c => new ClientResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .ToListAsync(cancellationToken);

        var totalCount = await db.Clients.CountAsync(cancellationToken);

        return new ListResult<ClientResult>(items, totalCount);
    }
}
```

> `ClientResult` (namespace `Fmis.Core.Clients`) resolves here without a `using` because `Fmis.Core.Clients` is an enclosing namespace of `Fmis.Core.Clients.ListClients`. The same holds for `ListClientsQuery` above and `GetClientHandler` in Task 8.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~ListClientsHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add ListClients slice with ListResult and total count"
```

---

## Task 8: GetClient slice (with not-found)

**Files:**
- Create: `backend/src/Fmis.Core/Clients/GetClient/GetClientQuery.cs`
- Create: `backend/src/Fmis.Core/Clients/GetClient/GetClientHandler.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/GetClientHandlerTests.cs`

Reuses the shared `ClientResult` created in Task 7 — get-by-id returns it in singular form.

- [ ] **Step 1: Write the failing test**

`backend/tests/Fmis.Core.Tests/Clients/GetClientHandlerTests.cs`:

```csharp
using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.GetClient;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class GetClientHandlerTests : InMemoryCoreTestBase
{
    [Fact]
    public async Task Returns_the_client_when_it_exists()
    {
        var created = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "ops@acme.example", null));

        var result = await QueryBus.QueryAsync(new GetClientQuery(created.Id));

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
        Assert.Equal("Acme Farms", result.Name);
    }

    [Fact]
    public async Task Returns_null_when_the_client_does_not_exist()
    {
        var result = await QueryBus.QueryAsync(new GetClientQuery(Guid.NewGuid()));

        Assert.Null(result);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~GetClientHandlerTests"`
Expected: FAIL — `GetClientQuery` does not exist. (`ClientResult` already exists from Task 7.)

- [ ] **Step 3: Create the query**

Returning `ClientResult?` makes "not found" an expected `null` the Api maps to 404.

`backend/src/Fmis.Core/Clients/GetClient/GetClientQuery.cs`:

```csharp
using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.GetClient;

public record GetClientQuery(Guid Id) : IQuery<ClientResult?>;
```

- [ ] **Step 4: Create the handler**

`backend/src/Fmis.Core/Clients/GetClient/GetClientHandler.cs`:

```csharp
using Fmis.Core.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.GetClient;

public class GetClientHandler(FmisDbContext db)
    : IQueryHandler<GetClientQuery, ClientResult?>
{
    public async Task<ClientResult?> HandleAsync(GetClientQuery query, CancellationToken cancellationToken)
    {
        return await db.Clients
            .Where(c => c.Id == query.Id)
            .Select(c => new ClientResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~GetClientHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add GetClient slice reusing shared ClientResult"
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

## Task 10: Api host — DI, ProblemDetails, OpenAPI, auth, validation mapping

**Files:**
- Create: `backend/src/Fmis.Api/Common/ValidationExceptionHandler.cs`
- Create: `backend/src/Fmis.Api/Configuration/ApiServiceCollectionExtensions.cs`
- Create: `backend/src/Fmis.Api/Configuration/ApiApplicationBuilderExtensions.cs`
- Modify: `backend/src/Fmis.Api/Program.cs` (replace template with thin host)
- Create/replace: `backend/src/Fmis.Api/appsettings.json`

- [ ] **Step 1: Create the validation exception handler**

Maps the FluentValidation `ValidationException` thrown by the command bus into a 400 with a validation-problem body.

`backend/src/Fmis.Api/Common/ValidationExceptionHandler.cs`:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Fmis.Api.Common;

public class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            new HttpValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest },
            cancellationToken);

        return true;
    }
}
```

- [ ] **Step 2: Create the service-registration extensions**

Keep `Program.cs` thin: group all service registration into cohesive extension methods.

`backend/src/Fmis.Api/Configuration/ApiServiceCollectionExtensions.cs`:

```csharp
using Fmis.Api.Common;
using Fmis.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Fmis.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFmisCore(configuration.GetConnectionString("Fmis")
            ?? throw new InvalidOperationException("Missing connection string 'Fmis'."));
        services.AddApiControllers();
        services.AddApiErrorHandling();
        services.AddApiDocumentation();
        services.AddApiAuthentication(configuration);
        return services;
    }

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);
        return services;
    }

    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Auth0:Authority"];
                options.Audience = configuration["Auth0:Audience"];
            });
        services.AddAuthorization();
        return services;
    }
}
```

- [ ] **Step 3: Create the application-pipeline extensions**

Group middleware, endpoint mapping, and the startup migration into pipeline extensions. `MigrateDatabase` guards on `IsRelational()` so it is a no-op under the InMemory provider the Api tests use (InMemory can't run migrations). `MapApiEndpoints` maps the MVC controllers, which are auto-discovered (no per-feature wiring needed).

`backend/src/Fmis.Api/Configuration/ApiApplicationBuilderExtensions.cs`:

```csharp
using Fmis.Core;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Api.Configuration;

public static class ApiApplicationBuilderExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapOpenApi();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapControllers();
        return app;
    }

    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FmisDbContext>();
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
        }
        return app;
    }
}
```

- [ ] **Step 4: Create the thin `Program.cs`**

`backend/src/Fmis.Api/Program.cs`:

```csharp
using Fmis.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.MigrateDatabase()
   .UseApiPipeline()
   .MapApiEndpoints();

app.Run();

public partial class Program;
```

> The `public partial class Program;` line exposes the otherwise-internal `Program` type so `Fmis.Api.Tests`' `WebApplicationFactory<Program>` can reference it.

- [ ] **Step 5: Replace `appsettings.json`**

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

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Api/Fmis.Api.csproj`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Fmis.Api/Program.cs backend/src/Fmis.Api/appsettings.json backend/src/Fmis.Api/Common/ backend/src/Fmis.Api/Configuration/
git commit -m "Configure Api host via thin Program.cs and service/pipeline extensions"
```

---

## Task 11: ClientsController (HTTP ↔ Models ↔ bus)

**Files:**
- Create: `backend/src/Fmis.Api/Clients/ClientsController.cs`

An MVC controller injects `ICommandBus`/`IQueryBus` (never handlers) and translates `Models` ↔ Core messages. It's auto-discovered by `MapControllers` (Task 10) — no wiring step. Validation is handled by the command bus (Task 2) and surfaced as 400 by the exception handler (Task 10) — the controller contains **no** manual validation. `[Authorize]` enforces authentication (an authenticated user); there are no authorization policies. Behavior is covered by the integration tests in Task 13.

- [ ] **Step 1: Create the controller**

`backend/src/Fmis.Api/Clients/ClientsController.cs`:

```csharp
using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.GetClient;
using Fmis.Core.Clients.ListClients;
using Fmis.Core.Common.Messaging;
using Fmis.Models.Clients;
using Fmis.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fmis.Api.Clients;

[ApiController]
[Route("clients")]
[Authorize]
public class ClientsController(ICommandBus commandBus, IQueryBus queryBus) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ClientResponseModel>> Create(
        [FromBody] CreateClientRequestModel request, CancellationToken cancellationToken)
    {
        var result = await commandBus.ExecuteAsync(
            new CreateClientCommand(request.Name, request.Email, request.PhoneNumber),
            cancellationToken);

        var model = new ClientResponseModel(result.Id, result.Name, result.Email, result.PhoneNumber);
        return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
    }

    [HttpGet]
    public async Task<ActionResult<ListResultModel<ClientResponseModel>>> List(CancellationToken cancellationToken)
    {
        var result = await queryBus.QueryAsync(new ListClientsQuery(), cancellationToken);

        var items = result.Items
            .Select(i => new ClientResponseModel(i.Id, i.Name, i.Email, i.PhoneNumber))
            .ToList();

        return new ListResultModel<ClientResponseModel>(items, result.TotalCount);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientResponseModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await queryBus.QueryAsync(new GetClientQuery(id), cancellationToken);

        return result is null
            ? NotFound()
            : new ClientResponseModel(result.Id, result.Name, result.Email, result.PhoneNumber);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Api/Fmis.Api.csproj`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add backend/src/Fmis.Api/
git commit -m "Add ClientsController injecting the command/query bus"
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
            services.RemoveAll(typeof(DbContextOptions<FmisDbContext>));
            services.AddDbContext<FmisDbContext>(options =>
                options.UseInMemoryDatabase($"fmis-api-tests-{Guid.NewGuid()}"));

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

These exercise the full HTTP pipeline (routing, auth, validation, bus, EF) via `Mvc.Testing` — authenticated success, 404, two 400 validation paths (blank name, no contact), and the 401 unauthenticated path.

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
        await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("Bedrock Ag", "info@bedrock.example", null));

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
            new CreateClientRequestModel("", "ops@acme.example", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_without_email_or_phone_returns_400()
    {
        var client = AuthenticatedClient();

        var response = await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("Acme Farms", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_authentication_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj`
Expected: PASS (6 tests). If the 401 test returns 404, confirm `[Authorize]` is on `ClientsController` and that `UseApiPipeline` runs `UseAuthentication()`/`UseAuthorization()`.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/Fmis.Api.Tests/Clients/
git commit -m "Add Client endpoint integration tests (success, 404, 400s, 401)"
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

Run: `dotnet test backend/Fmis.slnx`
Expected: ALL PASS (bus dispatch + validation, Core slices via the bus, Api integration incl. 401, OpenAPI smoke).

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
- Modify: `docker-compose.yml` (add `backend` service)

> Startup migration is already wired via `MigrateDatabase` (Task 10) and runs only under a relational provider, so no `Program.cs` change is needed here.

- [ ] **Step 1: Create the Dockerfile**

`backend/src/Fmis.Api/Dockerfile`:

```dockerfile
# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Fmis.slnx
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

- [ ] **Step 3: Add the backend service to `docker-compose.yml`**

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

- [ ] **Step 4: Build and run the stack**

```bash
docker compose up --build -d
```

Expected: both `db` and `backend` start; `docker compose ps` shows them healthy/running.

- [ ] **Step 5: Verify the API enforces auth and serves OpenAPI**

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/clients
```
Expected: `401`.

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/openapi/v1.json
```
Expected: `200`.

- [ ] **Step 6: Tear down**

```bash
docker compose down
```

- [ ] **Step 7: Commit**

```bash
git add backend/src/Fmis.Api/Dockerfile backend/.dockerignore docker-compose.yml
git commit -m "Add backend Dockerfile and docker-compose service"
```

---

## Done criteria

- `dotnet test backend/Fmis.slnx` passes all tests (bus dispatch + validation, Core slices via the bus, Api integration incl. 401 and both 400 validation paths, OpenAPI smoke).
- `docker compose up --build` runs db + backend; unauthenticated `/clients` returns 401; `/openapi/v1.json` returns 200.
- The Client slice exists end-to-end through Api → Models → bus (validate → dispatch) → Core handler → EF Core → Postgres, with authentication enforced and no authorization rules.
- Patterns established for Plan 2/3 to copy: vertical slice layout, in-house command/query bus with reflection discovery + command validation, handler-via-DI (never constructed), `*Entity`/`*Model`/`*Result` naming, generic `ListResult`/`ListResultModel`, Core DI composition root, `TestServices` (no-mocks, bus-driven tests), test auth scheme.
```
