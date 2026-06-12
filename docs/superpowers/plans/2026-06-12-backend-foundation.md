# Backend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working, authenticated ASP.NET Core API for managing `Client` records end-to-end (Create / List / Get-by-id), establishing the modular-monolith + vertical-slice + TDD patterns that every later phase copies.

**Architecture:** Modular monolith with vertical slices. `Fmis.Api` (HTTP) → `Fmis.Core` (vertical slices + EF Core) and `Fmis.Models` (external DTOs). Core knows nothing about HTTP or external DTOs; the Api translates between them. Persistence (EF Core / Npgsql) lives inside Core; feature handlers depend on `FmisDbContext` directly (no repository abstraction, no mocking frameworks). Authentication only (Auth0 JWT bearer) — no authorization rules.

**Tech Stack:** .NET 10 (`net10.0`), ASP.NET Core minimal APIs, EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL`, PostgreSQL+PostGIS (via Docker), xUnit, `Microsoft.AspNetCore.Mvc.Testing`, EF Core InMemory provider, `Testcontainers.PostgreSql`, built-in `Microsoft.AspNetCore.OpenApi`.

**Conventions (from `docs/conventions/`):** New commits only — never amend or force-push. No mocking frameworks. Do not split test projects by unit/integration. One test project per production project, plus a no-tests `Fmis.TestSupport`.

**Out of scope (later plans):** React frontend, Zod, Auth0 login UI, Playwright (Plan 2). Pulumi stacks, CI/CD, Azure deploy (Plan 3). PostGIS/spatial columns and the Ingestion project (later phases). The DB image is PostGIS-capable but no spatial features are used here.

---

## File Structure

```
backend/
├─ Fmis.sln
├─ src/
│  ├─ Fmis.Core/
│  │  ├─ Fmis.Core.csproj
│  │  ├─ FmisDbContext.cs                     EF Core context (DbSet<Client>)
│  │  ├─ FmisDbContextFactory.cs              design-time factory for migrations
│  │  ├─ CoreServiceCollectionExtensions.cs   AddFmisCore(...) DI wiring
│  │  ├─ Common/                              shared interfaces/base (empty for now)
│  │  ├─ Migrations/                          EF migrations (generated)
│  │  └─ Clients/
│  │     ├─ Client.cs                         entity
│  │     ├─ ClientResult.cs                   Core output record
│  │     ├─ ClientConfiguration.cs            IEntityTypeConfiguration<Client>
│  │     ├─ CreateClient/
│  │     │  ├─ CreateClientCommand.cs
│  │     │  └─ CreateClientHandler.cs
│  │     ├─ ListClients/
│  │     │  └─ ListClientsHandler.cs
│  │     └─ GetClient/
│  │        └─ GetClientHandler.cs
│  ├─ Fmis.Models/
│  │  ├─ Fmis.Models.csproj
│  │  └─ Clients/
│  │     ├─ CreateClientRequest.cs
│  │     └─ ClientResponse.cs
│  └─ Fmis.Api/
│     ├─ Fmis.Api.csproj
│     ├─ Program.cs                           DI, auth, ProblemDetails, OpenAPI, endpoints
│     ├─ appsettings.json                     connection string + Auth0 config
│     ├─ Dockerfile
│     └─ Clients/
│        └─ ClientEndpoints.cs                maps HTTP ↔ Models ↔ Core
└─ tests/
   ├─ Fmis.TestSupport/
   │  ├─ Fmis.TestSupport.csproj
   │  └─ TestDb.cs                            static factory: InMemory + Testcontainers
   ├─ Fmis.Core.Tests/
   │  ├─ Fmis.Core.Tests.csproj
   │  └─ Clients/
   │     ├─ CreateClientHandlerTests.cs
   │     ├─ ListClientsHandlerTests.cs
   │     └─ GetClientHandlerTests.cs
   └─ Fmis.Api.Tests/
      ├─ Fmis.Api.Tests.csproj
      ├─ TestAuthHandler.cs                   hand-written test auth scheme
      ├─ FmisApiFactory.cs                    WebApplicationFactory
      └─ Clients/
         └─ ClientEndpointsTests.cs
docker-compose.yml                            postgres+postgis, backend
```

---

## Task 1: Solution & project skeleton

**Files:**
- Create: `backend/Fmis.sln` and all `.csproj` files listed below.

- [ ] **Step 1: Create the solution and source projects**

Run from the repo root:

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

`Fmis.TestSupport` holds shared test utilities and contains **no tests** — but it is created from the xunit template so it has the test packages available for shared helpers. We will not add `[Fact]`s to it.

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
# Api depends on Core and Models
dotnet add src/Fmis.Api/Fmis.Api.csproj reference src/Fmis.Core/Fmis.Core.csproj src/Fmis.Models/Fmis.Models.csproj

# TestSupport depends on Core (for FmisDbContext)
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj reference src/Fmis.Core/Fmis.Core.csproj

# Core.Tests depends on Core + TestSupport
dotnet add tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj reference src/Fmis.Core/Fmis.Core.csproj tests/Fmis.TestSupport/Fmis.TestSupport.csproj

# Api.Tests depends on Api + TestSupport
dotnet add tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj reference src/Fmis.Api/Fmis.Api.csproj tests/Fmis.TestSupport/Fmis.TestSupport.csproj
```

- [ ] **Step 5: Add NuGet packages**

```bash
# Core: EF Core + Npgsql + design-time + DI abstractions
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.EntityFrameworkCore
dotnet add src/Fmis.Core/Fmis.Core.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/Fmis.Core/Fmis.Core.csproj package Microsoft.Extensions.DependencyInjection.Abstractions

# Api: JWT bearer + OpenAPI
dotnet add src/Fmis.Api/Fmis.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Fmis.Api/Fmis.Api.csproj package Microsoft.AspNetCore.OpenApi

# TestSupport: InMemory provider + Testcontainers
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj package Microsoft.EntityFrameworkCore.InMemory
dotnet add tests/Fmis.TestSupport/Fmis.TestSupport.csproj package Testcontainers.PostgreSql

# Api.Tests: Mvc.Testing
dotnet add tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 6: Build to verify the skeleton compiles**

Run: `dotnet build backend/Fmis.sln`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "Add backend solution skeleton (Core/Models/Api + test projects)"
```

---

## Task 2: Client entity, FmisDbContext, and Core DI wiring

**Files:**
- Create: `backend/src/Fmis.Core/Clients/Client.cs`
- Create: `backend/src/Fmis.Core/Clients/ClientResult.cs`
- Create: `backend/src/Fmis.Core/Clients/ClientConfiguration.cs`
- Create: `backend/src/Fmis.Core/FmisDbContext.cs`
- Create: `backend/src/Fmis.Core/FmisDbContextFactory.cs`
- Create: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`

This task is plumbing with no behavior to test yet; the round-trip test arrives in Task 3 once the test factory exists.

- [ ] **Step 1: Create the `Client` entity**

`backend/src/Fmis.Core/Clients/Client.cs`:

```csharp
namespace Fmis.Core.Clients;

public class Client
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}
```

- [ ] **Step 2: Create the Core output record**

The Core layer returns its own type — never the entity and never a `Models` DTO — so the external contract can evolve independently.

`backend/src/Fmis.Core/Clients/ClientResult.cs`:

```csharp
namespace Fmis.Core.Clients;

public record ClientResult(Guid Id, string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 3: Create the entity configuration**

Configuration lives with the slice, not in the DbContext.

`backend/src/Fmis.Core/Clients/ClientConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fmis.Core.Clients;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.Property(c => c.PhoneNumber).HasMaxLength(50);
    }
}
```

- [ ] **Step 4: Create the DbContext**

`backend/src/Fmis.Core/FmisDbContext.cs`:

```csharp
using Fmis.Core.Clients;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core;

public class FmisDbContext(DbContextOptions<FmisDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FmisDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 5: Create the design-time factory (for `dotnet ef migrations`)**

This lets migrations be generated without building the Api. The connection string is design-time only and matches `docker-compose.yml` (Task 4).

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

- [ ] **Step 6: Create the Core DI extension**

Core owns its own wiring (DbContext + handlers). Handlers are added in later tasks; the method exists now so the Api can call it.

`backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddFmisCore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FmisDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }
}
```

- [ ] **Step 7: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Core/Fmis.Core.csproj`
Expected: `Build succeeded`.

- [ ] **Step 8: Commit**

```bash
git add backend/src/Fmis.Core/
git commit -m "Add Client entity, FmisDbContext, and Core DI wiring"
```

---

## Task 3: TestSupport DbContext factory (InMemory + Testcontainers)

**Files:**
- Create: `backend/tests/Fmis.TestSupport/TestDb.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs` *(temporary round-trip test; the real handler test replaces its body in Task 5 — for now it proves the factory works)*

The factory is the shared way every test gets a `FmisDbContext`. `InMemory()` is for fast tests; `ContainerAsync()` spins a real PostGIS container and applies migrations. Each test picks what it needs. No mocking framework is involved — both return a real `FmisDbContext`.

- [ ] **Step 1: Write the factory**

`backend/tests/Fmis.TestSupport/TestDb.cs`:

```csharp
using Fmis.Core;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Fmis.TestSupport;

/// <summary>
/// Shared factory for creating a real <see cref="FmisDbContext"/> in tests.
/// Use <see cref="InMemory"/> for fast tests, <see cref="ContainerAsync"/> when a
/// real Postgres/PostGIS database is required. No mocking — both are real contexts.
/// </summary>
public static class TestDb
{
    /// <summary>Creates a context on the EF Core InMemory provider with a unique database name.</summary>
    public static FmisDbContext InMemory()
    {
        var options = new DbContextOptionsBuilder<FmisDbContext>()
            .UseInMemoryDatabase($"fmis-test-{Guid.NewGuid()}")
            .Options;
        var context = new FmisDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Starts a disposable PostGIS container, applies migrations, and returns a connected context.
    /// Dispose the returned handle to stop the container.
    /// </summary>
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

/// <summary>Owns a running container and its context; dispose to tear both down.</summary>
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

- [ ] **Step 2: Write a failing test that uses the InMemory factory**

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

        db.Clients.Add(new Client { Id = Guid.NewGuid(), Name = "Acme Farms" });
        await db.SaveChangesAsync();

        Assert.Single(db.Clients);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails to build/pass**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj`
Expected: FAILS to compile until `TestDb.cs` is referenced correctly, then PASSES once `TestDb` resolves. If it already passes, the factory is wired correctly.

> Note: this is a scaffolding test confirming the factory. Task 5 replaces this file's contents with the real `CreateClientHandler` tests.

- [ ] **Step 4: Run the full backend test suite**

Run: `dotnet test backend/Fmis.sln`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add backend/tests/Fmis.TestSupport/ backend/tests/Fmis.Core.Tests/
git commit -m "Add TestDb factory (InMemory + Testcontainers) with round-trip test"
```

---

## Task 4: Local Postgres+PostGIS, initial migration, and container-backed test

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

> The backend service is added in Task 14 once the Api runs.

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

        db.Context.Clients.Add(new Client { Id = Guid.NewGuid(), Name = "Container Farm" });
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

## Task 5: CreateClient slice

**Files:**
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientCommand.cs`
- Create: `backend/src/Fmis.Core/Clients/CreateClient/CreateClientHandler.cs`
- Modify: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs` (register handler)
- Test: `backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs` (replace scaffolding contents)

- [ ] **Step 1: Replace the scaffolding test with the real CreateClient test**

`backend/tests/Fmis.Core.Tests/Clients/CreateClientHandlerTests.cs`:

```csharp
using Fmis.Core.Clients.CreateClient;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class CreateClientHandlerTests
{
    [Fact]
    public async Task Persists_the_client_and_returns_it_with_a_generated_id()
    {
        await using var db = TestDb.InMemory();
        var handler = new CreateClientHandler(db);

        var result = await handler.Handle(
            new CreateClientCommand("Acme Farms", "ops@acme.example", "555-0100"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Acme Farms", result.Name);
        Assert.Equal("ops@acme.example", result.Email);
        Assert.Equal("555-0100", result.PhoneNumber);

        var saved = Assert.Single(db.Clients);
        Assert.Equal(result.Id, saved.Id);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CreateClientHandlerTests"`
Expected: FAIL — `CreateClientCommand` / `CreateClientHandler` do not exist (compile error).

- [ ] **Step 3: Create the command**

`backend/src/Fmis.Core/Clients/CreateClient/CreateClientCommand.cs`:

```csharp
namespace Fmis.Core.Clients.CreateClient;

public record CreateClientCommand(string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 4: Create the handler**

`backend/src/Fmis.Core/Clients/CreateClient/CreateClientHandler.cs`:

```csharp
namespace Fmis.Core.Clients.CreateClient;

public class CreateClientHandler(FmisDbContext db)
{
    public async Task<ClientResult> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Email = command.Email,
            PhoneNumber = command.PhoneNumber,
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);

        return new ClientResult(client.Id, client.Name, client.Email, client.PhoneNumber);
    }
}
```

- [ ] **Step 5: Register the handler in the Core DI extension**

`backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs` — add the handler registration:

```csharp
using Fmis.Core.Clients.CreateClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddFmisCore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FmisDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<CreateClientHandler>();
        return services;
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~CreateClientHandlerTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add CreateClient slice (command, handler, DI)"
```

---

## Task 6: ListClients slice

**Files:**
- Create: `backend/src/Fmis.Core/Clients/ListClients/ListClientsHandler.cs`
- Modify: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/ListClientsHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

`backend/tests/Fmis.Core.Tests/Clients/ListClientsHandlerTests.cs`:

```csharp
using Fmis.Core.Clients;
using Fmis.Core.Clients.ListClients;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class ListClientsHandlerTests
{
    [Fact]
    public async Task Returns_all_clients_as_results()
    {
        await using var db = TestDb.InMemory();
        db.Clients.Add(new Client { Id = Guid.NewGuid(), Name = "Acme Farms" });
        db.Clients.Add(new Client { Id = Guid.NewGuid(), Name = "Bedrock Ag" });
        await db.SaveChangesAsync();

        var handler = new ListClientsHandler(db);
        var results = await handler.Handle(CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "Acme Farms");
        Assert.Contains(results, r => r.Name == "Bedrock Ag");
    }

    [Fact]
    public async Task Returns_empty_list_when_there_are_no_clients()
    {
        await using var db = TestDb.InMemory();
        var handler = new ListClientsHandler(db);

        var results = await handler.Handle(CancellationToken.None);

        Assert.Empty(results);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~ListClientsHandlerTests"`
Expected: FAIL — `ListClientsHandler` does not exist.

- [ ] **Step 3: Create the handler**

`backend/src/Fmis.Core/Clients/ListClients/ListClientsHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.ListClients;

public class ListClientsHandler(FmisDbContext db)
{
    public async Task<IReadOnlyList<ClientResult>> Handle(CancellationToken cancellationToken)
    {
        return await db.Clients
            .OrderBy(c => c.Name)
            .Select(c => new ClientResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Register the handler**

In `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`, add the using and registration:

```csharp
using Fmis.Core.Clients.ListClients;
```

and inside `AddFmisCore`, after the `CreateClientHandler` line:

```csharp
        services.AddScoped<ListClientsHandler>();
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~ListClientsHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add ListClients slice"
```

---

## Task 7: GetClient slice (with not-found)

**Files:**
- Create: `backend/src/Fmis.Core/Clients/GetClient/GetClientHandler.cs`
- Modify: `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`
- Test: `backend/tests/Fmis.Core.Tests/Clients/GetClientHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

`backend/tests/Fmis.Core.Tests/Clients/GetClientHandlerTests.cs`:

```csharp
using Fmis.Core.Clients;
using Fmis.Core.Clients.GetClient;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class GetClientHandlerTests
{
    [Fact]
    public async Task Returns_the_client_when_it_exists()
    {
        await using var db = TestDb.InMemory();
        var id = Guid.NewGuid();
        db.Clients.Add(new Client { Id = id, Name = "Acme Farms", Email = "ops@acme.example" });
        await db.SaveChangesAsync();

        var handler = new GetClientHandler(db);
        var result = await handler.Handle(id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal("Acme Farms", result.Name);
    }

    [Fact]
    public async Task Returns_null_when_the_client_does_not_exist()
    {
        await using var db = TestDb.InMemory();
        var handler = new GetClientHandler(db);

        var result = await handler.Handle(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~GetClientHandlerTests"`
Expected: FAIL — `GetClientHandler` does not exist.

- [ ] **Step 3: Create the handler**

Returning `null` for not-found (not throwing) keeps "not found" an expected result the Api maps to 404.

`backend/src/Fmis.Core/Clients/GetClient/GetClientHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.GetClient;

public class GetClientHandler(FmisDbContext db)
{
    public async Task<ClientResult?> Handle(Guid id, CancellationToken cancellationToken)
    {
        return await db.Clients
            .Where(c => c.Id == id)
            .Select(c => new ClientResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Register the handler**

In `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`, add:

```csharp
using Fmis.Core.Clients.GetClient;
```

and inside `AddFmisCore`:

```csharp
        services.AddScoped<GetClientHandler>();
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test backend/tests/Fmis.Core.Tests/Fmis.Core.Tests.csproj --filter "FullyQualifiedName~GetClientHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Fmis.Core/ backend/tests/Fmis.Core.Tests/
git commit -m "Add GetClient slice with not-found handling"
```

---

## Task 8: External API Models (DTOs)

**Files:**
- Create: `backend/src/Fmis.Models/Clients/CreateClientRequest.cs`
- Create: `backend/src/Fmis.Models/Clients/ClientResponse.cs`

These are the external contract the Api exposes (and that the frontend Zod schemas will mirror in Plan 2). They are deliberately separate from the Core `ClientResult` so the contract can evolve independently.

- [ ] **Step 1: Create the request DTO**

`backend/src/Fmis.Models/Clients/CreateClientRequest.cs`:

```csharp
namespace Fmis.Models.Clients;

public record CreateClientRequest(string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 2: Create the response DTO**

`backend/src/Fmis.Models/Clients/ClientResponse.cs`:

```csharp
namespace Fmis.Models.Clients;

public record ClientResponse(Guid Id, string Name, string? Email, string? PhoneNumber);
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Models/Fmis.Models.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Fmis.Models/
git commit -m "Add Client external API models (request/response DTOs)"
```

---

## Task 9: Api host — DI, ProblemDetails, OpenAPI, and authentication

**Files:**
- Modify: `backend/src/Fmis.Api/Program.cs` (replace template)
- Create: `backend/src/Fmis.Api/appsettings.json` (replace template contents)

This task stands up the host with the cross-cutting concerns. Endpoints arrive in Task 10. We make `Program` reachable from tests by ensuring the implicit `Program` class is public (achieved via a `public partial class Program` declaration).

- [ ] **Step 1: Replace `Program.cs`**

`backend/src/Fmis.Api/Program.cs`:

```csharp
using Fmis.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Core (EF Core + handlers)
builder.Services.AddFmisCore(builder.Configuration.GetConnectionString("Fmis")
    ?? throw new InvalidOperationException("Missing connection string 'Fmis'."));

// Consistent error shape (RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();

// OpenAPI document at /openapi/v1.json
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

// Client endpoints are mapped here in Task 10:
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

> `Auth0:Authority`/`Audience` are blank locally; the real values come from the `auth` Pulumi stack in Plan 3. With no token presented, protected endpoints return 401 without contacting Auth0.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Api/Fmis.Api.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Fmis.Api/Program.cs backend/src/Fmis.Api/appsettings.json
git commit -m "Configure Api host: Core DI, ProblemDetails, OpenAPI, JWT auth"
```

---

## Task 10: Client endpoints (HTTP ↔ Models ↔ Core)

**Files:**
- Create: `backend/src/Fmis.Api/Clients/ClientEndpoints.cs`
- Modify: `backend/src/Fmis.Api/Program.cs` (map the endpoints)

The endpoints translate HTTP + `Models` DTOs into Core handler calls and map Core `ClientResult` back to `ClientResponse`. All endpoints require an authenticated user. Validation happens here (before Core) and returns a 400 ValidationProblem. Endpoint behavior is covered by the integration tests in Task 12.

- [ ] **Step 1: Create the endpoints**

`backend/src/Fmis.Api/Clients/ClientEndpoints.cs`:

```csharp
using Fmis.Core.Clients;
using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.GetClient;
using Fmis.Core.Clients.ListClients;
using Fmis.Models.Clients;

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
        CreateClientRequest request,
        CreateClientHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."],
            });
        }

        var result = await handler.Handle(
            new CreateClientCommand(request.Name, request.Email, request.PhoneNumber),
            cancellationToken);

        return Results.Created($"/clients/{result.Id}", ToResponse(result));
    }

    private static async Task<IResult> ListClients(
        ListClientsHandler handler,
        CancellationToken cancellationToken)
    {
        var results = await handler.Handle(cancellationToken);
        return Results.Ok(results.Select(ToResponse));
    }

    private static async Task<IResult> GetClient(
        Guid id,
        GetClientHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(ToResponse(result));
    }

    private static ClientResponse ToResponse(ClientResult result) =>
        new(result.Id, result.Name, result.Email, result.PhoneNumber);
}
```

- [ ] **Step 2: Map the endpoints in `Program.cs`**

In `backend/src/Fmis.Api/Program.cs`, replace the commented line

```csharp
// Client endpoints are mapped here in Task 10:
// app.MapClientEndpoints();
```

with:

```csharp
app.MapClientEndpoints();
```

and add the using at the top of the file:

```csharp
using Fmis.Api.Clients;
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/src/Fmis.Api/Fmis.Api.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Fmis.Api/
git commit -m "Add Client endpoints mapping HTTP/Models to Core handlers"
```

---

## Task 11: Test authentication scheme + WebApplicationFactory

**Files:**
- Create: `backend/tests/Fmis.Api.Tests/TestAuthHandler.cs`
- Create: `backend/tests/Fmis.Api.Tests/FmisApiFactory.cs`

The Api tests must not call real Auth0. Instead the factory swaps in a hand-written "Test" authentication scheme (no mocking framework). The handler authenticates only when an `Authorization` header is present, so tests control authenticated vs. unauthenticated by setting (or omitting) that header. The factory also points the DbContext at a fresh InMemory database so endpoint tests are fast and isolated.

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
        // Authenticate only when the caller presents an Authorization header.
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

> `AddAuthentication(scheme)` here re-sets the default scheme to `Test`, so the endpoints' `RequireAuthorization()` is satisfied by the test handler. The InMemory database ensures the schema exists without a container.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build backend/tests/Fmis.Api.Tests/Fmis.Api.Tests.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add backend/tests/Fmis.Api.Tests/TestAuthHandler.cs backend/tests/Fmis.Api.Tests/FmisApiFactory.cs
git commit -m "Add test auth scheme and WebApplicationFactory for Api tests"
```

---

## Task 12: Client endpoint integration tests

**Files:**
- Create: `backend/tests/Fmis.Api.Tests/Clients/ClientEndpointsTests.cs`

These exercise the full HTTP pipeline (routing, auth, validation, Core, EF) via `Mvc.Testing`. They assert authenticated success paths, 404, 400 validation, and the 401 unauthenticated path.

- [ ] **Step 1: Write the failing tests**

`backend/tests/Fmis.Api.Tests/Clients/ClientEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fmis.Models.Clients;

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
            new CreateClientRequest("Acme Farms", "ops@acme.example", "555-0100"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);

        var get = await client.GetAsync($"/clients/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var fetched = await get.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.Equal("Acme Farms", fetched!.Name);
    }

    [Fact]
    public async Task List_returns_created_clients()
    {
        var client = AuthenticatedClient();
        await client.PostAsJsonAsync("/clients", new CreateClientRequest("Bedrock Ag", null, null));

        var list = await client.GetFromJsonAsync<List<ClientResponse>>("/clients");

        Assert.NotNull(list);
        Assert.Contains(list!, c => c.Name == "Bedrock Ag");
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
            new CreateClientRequest("", null, null));

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
Expected: PASS (5 tests). If the 401 test fails with 404, confirm `RequireAuthorization()` is on the group and `UseAuthentication()/UseAuthorization()` are in `Program.cs`.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/Fmis.Api.Tests/Clients/
git commit -m "Add Client endpoint integration tests (success, 404, 400, 401)"
```

---

## Task 13: OpenAPI document smoke test

**Files:**
- Create: `backend/tests/Fmis.Api.Tests/OpenApiTests.cs`

The OpenAPI document is the artifact Plan 2's Zod↔OpenAPI contract test verifies against. This smoke test confirms it is served.

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
Expected: PASS. (If `/clients` is absent from the document, confirm `MapOpenApi()` runs and endpoints are mapped before `app.Run()`.)

- [ ] **Step 3: Run the entire backend suite**

Run: `dotnet test backend/Fmis.sln`
Expected: ALL PASS (Core handler tests, schema/container test, Api integration tests, OpenAPI test).

- [ ] **Step 4: Commit**

```bash
git add backend/tests/Fmis.Api.Tests/OpenApiTests.cs
git commit -m "Add OpenAPI document smoke test"
```

---

## Task 14: Backend Dockerfile and docker-compose service

**Files:**
- Create: `backend/src/Fmis.Api/Dockerfile`
- Create: `backend/.dockerignore`
- Modify: `docker-compose.yml` (add `backend` service)

This makes `docker compose up` run the database + API together — the local full-stack base the frontend joins in Plan 2.

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

- [ ] **Step 4: Apply migrations are needed before the API serves data — add a startup migration call**

So the containerized API creates its schema on boot, apply migrations at startup. First add `using Microsoft.EntityFrameworkCore;` to the top of `backend/src/Fmis.Api/Program.cs` (needed for the `Migrate()` extension). Then, immediately after `var app = builder.Build();`, add:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Fmis.Core.FmisDbContext>();
    db.Database.Migrate();
}
```

- [ ] **Step 5: Build and run the stack**

```bash
docker compose up --build -d
```

Expected: both `db` and `backend` start; `docker compose ps` shows them healthy/running.

- [ ] **Step 6: Verify the API responds with 401 (auth enforced) when unauthenticated**

Run: `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/clients`
Expected: `401`.

Run: `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/openapi/v1.json`
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

- `dotnet test backend/Fmis.sln` passes all tests (Core handlers, real-container schema test, Api integration incl. 401, OpenAPI smoke).
- `docker compose up --build` runs db + backend; unauthenticated `/clients` returns 401; `/openapi/v1.json` returns 200.
- The Client slice exists end-to-end through Api → Models → Core → EF Core → Postgres, with authentication enforced and no authorization rules.
- Patterns established for Plan 2/3 to copy: vertical slice layout, Core DI extension, handler-per-feature, Models↔Core mapping, TestDb factory, no-mocks testing, test auth scheme.
```
