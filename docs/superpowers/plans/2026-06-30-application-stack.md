# Application Stack (Phase 3b‑2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run modern-fmis on Azure — the backend on Container Apps (managed-identity Postgres, no password), the frontend as a Blob Storage static site with a Pulumi-generated `config.json` — built, pushed, and deployed by Pulumi.

**Architecture:** Part A is two small, config-driven backend changes (Entra-token Postgres auth + CORS) that leave local `docker compose` and the contract test untouched. Part B is a new `infra/application` Pulumi project — a thin `ApplicationStack` over `ContainerRegistry` + `BackendApp` + `FrontendSite` components — reading the `auth`/`persistence` stacks via stack references, with Pulumi building/pushing the backend image inside `pulumi up`, plus a CD `apps` job.

**Tech Stack:** .NET 10, ASP.NET Core, Npgsql + `Azure.Identity`; Pulumi C# + `Pulumi.AzureNative` (ACR / Container Apps / Storage / Authorization) + the Pulumi docker image-build provider + a synced-folder mechanism; `Pulumi.Testing` + xUnit.

## Global Constraints

- **No code comments** — self-documenting names (backend + infra).
- **Exact pinned package versions** — no floating ranges; pin to the resolved version after adding.
- **TDD** — failing test seen first; minimal code to pass.
- **Run `dotnet` from the repo root** (`Fmis.slnx`); all shell via `zsh -lc`.
- **New commits only** — never amend/force-push.
- **ComponentResource composition** — sealed components, type token `fmis:application:<Type>`, children parented via `new CustomResourceOptions { Parent = this }`, `RegisterOutputs()`; thin `ApplicationStack` root.
- **Naming** via `Fmis.Infra.Common.ResourceNames.For(env, "application", …)`; default region `centralus`.
- **Provider/Azure API is version-sensitive** — `Pulumi.AzureNative` (Container Apps, Storage static website, ACR, RoleAssignment), the docker image-build provider, the synced-folder provider, and the `StackReference` test-mock shape must be verified against the installed package versions and adjusted; the **test assertions are the load-bearing contract**.

---

## File structure

```
backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs   # AddFmisCore: token-vs-password data source
backend/src/Fmis.Core/FmisDataSource.cs                    # NEW: builds the NpgsqlDataSource (testable seam)
backend/src/Fmis.Core/Fmis.Core.csproj                     # + Azure.Identity, Npgsql.DependencyInjection
backend/src/Fmis.Api/Configuration/ApiServiceCollectionExtensions.cs  # pass flag/clientId; AddCors
backend/src/Fmis.Api/Configuration/ApiApplicationBuilderExtensions.cs # UseCors
backend/tests/Fmis.Core.Tests/CoreDataSourceTests.cs       # NEW: data-source selection
backend/tests/Fmis.Api.Tests/CorsTests.cs                  # NEW: preflight headers
infra/application/                                          # NEW Pulumi project (mirror infra/persistence)
  Fmis.Infra.Application.csproj · Pulumi.yaml · Pulumi.dev.yaml · Program.cs · ApplicationStack.cs
  Components/{ContainerRegistry,BackendApp,FrontendSite}.cs
infra/tests/Fmis.Infra.Tests/StackMocks.cs                 # + StackReference mocking
infra/tests/Fmis.Infra.Tests/InfraTesting.cs               # + RunApplicationStackAsync
infra/tests/Fmis.Infra.Tests/ApplicationStackTests.cs      # NEW
Fmis.slnx                                                   # + the application project
.github/workflows/cd.yml                                   # real apps job
```

---

# PART A — Backend code changes

## Task A1: Entra-token Postgres auth (config-selected)

**Files:** Create `backend/src/Fmis.Core/FmisDataSource.cs`, `backend/tests/Fmis.Core.Tests/CoreDataSourceTests.cs`; Modify `backend/src/Fmis.Core/CoreServiceCollectionExtensions.cs`, `backend/src/Fmis.Core/Fmis.Core.csproj`, `backend/src/Fmis.Api/Configuration/ApiServiceCollectionExtensions.cs`.

**Interfaces:**
- Produces: `AddFmisCore(this IServiceCollection, FmisDatabaseOptions options)` where `FmisDatabaseOptions` is `record FmisDatabaseOptions(string ConnectionString, bool UseEntraAuth, string? ManagedIdentityClientId)`; `FmisDataSource.Build(FmisDatabaseOptions) : NpgsqlDataSource`.

- [ ] **Step 1: Add packages** (pin to resolved versions)
```bash
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet add backend/src/Fmis.Core/Fmis.Core.csproj package Azure.Identity'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet add backend/src/Fmis.Core/Fmis.Core.csproj package Npgsql.DependencyInjection'
```

- [ ] **Step 2: Failing test** — `backend/tests/Fmis.Core.Tests/CoreDataSourceTests.cs`:
```csharp
using Fmis.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests;

public class CoreDataSourceTests
{
    [Fact]
    public void Password_mode_resolves_the_db_context()
    {
        var services = new ServiceCollection();

        services.AddFmisCore(new FmisDatabaseOptions(
            "Host=localhost;Database=fmis;Username=fmis;Password=fmis", false, null));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<FmisDbContext>());
    }

    [Fact]
    public void Entra_mode_resolves_the_db_context_without_a_password()
    {
        var services = new ServiceCollection();

        services.AddFmisCore(new FmisDatabaseOptions(
            "Host=fmis-dev-persistence-postgres.postgres.database.azure.com;Database=fmis;Username=fmis-dev-app-identity;Ssl Mode=Require",
            true, "00000000-0000-0000-0000-000000000001"));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<FmisDbContext>());
    }
}
```
(Resolving the context does NOT open a connection, so Entra mode builds the token data source without authenticating — honest offline. The real token fetch is deploy-verified.)

- [ ] **Step 3: Run red** → `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter "FullyQualifiedName~CoreDataSourceTests"'` → FAIL (no `FmisDatabaseOptions`).

- [ ] **Step 4: Implement** — `backend/src/Fmis.Core/FmisDataSource.cs`:
```csharp
using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace Fmis.Core;

public record FmisDatabaseOptions(string ConnectionString, bool UseEntraAuth, string? ManagedIdentityClientId);

public static class FmisDataSource
{
    private static readonly string[] Scope = ["https://ossrdbms-aad.database.windows.net/.default"];

    public static NpgsqlDataSource Build(FmisDatabaseOptions options)
    {
        var builder = new NpgsqlDataSourceBuilder(options.ConnectionString);
        if (options.UseEntraAuth)
        {
            var credential = new DefaultAzureCredential(
                new DefaultAzureCredentialOptions { ManagedIdentityClientId = options.ManagedIdentityClientId });
            builder.UsePeriodicPasswordProvider(
                async (_, cancellationToken) =>
                {
                    var token = await credential.GetTokenAsync(new TokenRequestContext(Scope), cancellationToken);
                    return token.Token;
                },
                TimeSpan.FromMinutes(55),
                TimeSpan.FromSeconds(5));
        }
        return builder.Build();
    }
}
```
Change `CoreServiceCollectionExtensions.AddFmisCore` to accept `FmisDatabaseOptions` and register via the data source:
```csharp
    public static IServiceCollection AddFmisCore(this IServiceCollection services, FmisDatabaseOptions options)
    {
        var dataSource = FmisDataSource.Build(options);
        services.AddDbContext<FmisDbContext>(dbOptions => dbOptions.UseNpgsql(dataSource));
        return services.AddFmisCoreHandlers();
    }
```
Update `ApiServiceCollectionExtensions.AddApiServices` to build the options from config:
```csharp
        services.AddFmisCore(new Fmis.Core.FmisDatabaseOptions(
            configuration.GetConnectionString("Fmis") ?? throw new InvalidOperationException("Missing connection string 'Fmis'."),
            configuration.GetValue("Database:UseEntraAuth", false),
            configuration["AZURE_CLIENT_ID"]));
```
Verify `UsePeriodicPasswordProvider` exists in the installed Npgsql (it's on `NpgsqlDataSourceBuilder`); adjust the method/signature to the installed version if needed. Pin the two new packages in the csproj.

- [ ] **Step 5: Run green** → same filter passes; then `dotnet test Fmis.slnx --filter "FullyQualifiedName~Fmis.Core.Tests|FullyQualifiedName~Fmis.Api.Tests"` → all green (the api factory still overrides EF with InMemory, so its `AddFmisCore` change compiles + runs).

- [ ] **Step 6: Commit**
```bash
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add backend && git commit -m "Add config-selected Entra-token Postgres auth (local keeps password) (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"'
```

## Task A2: CORS for the static-site origin

**Files:** Create `backend/tests/Fmis.Api.Tests/CorsTests.cs`; Modify `backend/src/Fmis.Api/Configuration/ApiServiceCollectionExtensions.cs`, `backend/src/Fmis.Api/Configuration/ApiApplicationBuilderExtensions.cs`.

**Interfaces:**
- Consumes: `Cors:AllowedOrigin` from configuration. Produces: a named CORS policy `"Spa"` applied in the pipeline before authentication/controllers.

- [ ] **Step 1: Failing test** — `backend/tests/Fmis.Api.Tests/CorsTests.cs`. Configure the test host with `Cors:AllowedOrigin` and assert the allow-origin header on a preflight:
```csharp
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Fmis.Api.Tests;

public class CorsTests
{
    [Fact]
    public async Task Allows_the_configured_spa_origin()
    {
        await using var factory = new FmisApiFactory()
            .WithConfig("Cors:AllowedOrigin", "https://fmisdevweb.z13.web.core.windows.net");
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/clients");
        request.Headers.Add("Origin", "https://fmisdevweb.z13.web.core.windows.net");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await client.SendAsync(request);

        Assert.Contains("https://fmisdevweb.z13.web.core.windows.net",
            response.Headers.GetValues("Access-Control-Allow-Origin"));
    }
}
```
Add a small `WithConfig` helper to `FmisApiFactory` (override `ConfigureWebHost` to also `ConfigureAppConfiguration` with an in-memory pair, returning `this`):
```csharp
    private readonly Dictionary<string, string?> overrides = new();
    public FmisApiFactory WithConfig(string key, string value) { overrides[key] = value; return this; }
```
and inside `ConfigureWebHost`, before `ConfigureTestServices`, add `builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(overrides));`. Verify the exact route (`/api/clients`) against the existing controller route; use a real route from `ClientsController`.

- [ ] **Step 2: Run red** → `dotnet test Fmis.slnx --filter "FullyQualifiedName~CorsTests"` → FAIL (no allow-origin header).

- [ ] **Step 3: Implement** — in `ApiServiceCollectionExtensions`, add a CORS service registration reading the origin:
```csharp
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origin = configuration["Cors:AllowedOrigin"];
        services.AddCors(options => options.AddPolicy("Spa", policy =>
        {
            if (!string.IsNullOrWhiteSpace(origin))
                policy.WithOrigins(origin).AllowAnyHeader().AllowAnyMethod();
        }));
        return services;
    }
```
Call `services.AddApiCors(configuration);` from `AddApiServices`. In `ApiApplicationBuilderExtensions.UseApiPipeline`, add `app.UseCors("Spa");` **before** `UseAuthentication`/`UseAuthorization` and the controller mapping (read the file to place it correctly).

- [ ] **Step 4: Run green** → the filter passes; then `dotnet test Fmis.slnx --filter "FullyQualifiedName~Fmis.Api.Tests"` → all green (unset origin → no CORS headers, existing tests unaffected).

- [ ] **Step 5: Commit**
```bash
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add backend && git commit -m "Allow the configured SPA origin via CORS (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"'
```

---

# PART B — Application Pulumi stack + CD

## Task B1: Scaffold the application project

**Files:** Create `infra/application/{Fmis.Infra.Application.csproj,Pulumi.yaml,Program.cs,ApplicationStack.cs}`; Modify `Fmis.slnx`, `infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj`.

- [ ] **Step 1: Create + packages + references** (mirror `infra/persistence`):
```bash
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra && mkdir -p application && cd application && dotnet new console -n Fmis.Infra.Application -o . && rm -f Program.cs'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra/application && dotnet add Fmis.Infra.Application.csproj package Pulumi'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra/application && dotnet add Fmis.Infra.Application.csproj package Pulumi.AzureNative'
# Image build provider — verify the current package name; prefer Pulumi.DockerBuild, else Pulumi.Docker:
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra/application && dotnet add Fmis.Infra.Application.csproj package Pulumi.DockerBuild'
# Static-asset upload — verify; prefer the synced-folder provider, else fall back to per-file Blob resources (no package):
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra/application && dotnet add Fmis.Infra.Application.csproj package Pulumi.SyncedFolder'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra/application && dotnet add Fmis.Infra.Application.csproj reference ../Fmis.Infra.Common/Fmis.Infra.Common.csproj'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet sln Fmis.slnx add infra/application/Fmis.Infra.Application.csproj'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet add infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj reference infra/application/Fmis.Infra.Application.csproj'
```
Pin every package to its resolved version; trim inherited props (match `infra/persistence/Fmis.Infra.Persistence.csproj`); move the project into the `/infra/` solution folder in `Fmis.slnx`. If `Pulumi.SyncedFolder`/`Pulumi.DockerBuild` don't resolve, note it in the task report and use the fallback (per-file `Blob` upload of `dist/`; `Pulumi.Docker.Image`) — adjust later tasks accordingly.

`Pulumi.yaml`:
```yaml
name: fmis-application
runtime: dotnet
description: Backend Container App + frontend static site + runtime config.json
```
`ApplicationStack.cs`: `public class ApplicationStack : Stack { public ApplicationStack() { } }`.
`Program.cs`: `return await Pulumi.Deployment.RunAsync<Fmis.Infra.Application.ApplicationStack>();`

- [ ] **Step 2: Build** → `dotnet build Fmis.slnx` succeeds.
- [ ] **Step 3: Commit** → `git add infra Fmis.slnx && git commit -m "Scaffold infra/application Pulumi project ..."`.

## Task B2: Test harness — RunApplicationStackAsync + StackReference mocking

**Files:** Modify `infra/tests/Fmis.Infra.Tests/StackMocks.cs`, `infra/tests/Fmis.Infra.Tests/InfraTesting.cs`.

**Interfaces:**
- Produces: `InfraTesting.RunApplicationStackAsync() : Task<ImmutableArray<Resource>>`; `StackMocks` returns fake `outputs` for `pulumi:pulumi:StackReference` resources keyed by the referenced stack name.

- [ ] **Step 1: Mock stack references in `StackMocks`.** A Pulumi `StackReference` registers a resource of type `pulumi:pulumi:StackReference` whose `outputs` property supplies the referenced stack's outputs. In `StackMocks.NewResourceAsync`, before the generic name-resolution, return fake outputs when the type is a stack reference (match on the stack name carried in `args.Name` or `args.Inputs["name"]`):
```csharp
        if (args.Type == "pulumi:pulumi:StackReference")
        {
            var referenced = args.Name;
            var outputs = referenced.Contains("auth")
                ? new Dictionary<string, object>
                {
                    ["domain"] = "fmis-dev.us.auth0.com",
                    ["spaClientId"] = "spa-client-id",
                    ["audience"] = "https://dev.api.modern-fmis",
                }
                : new Dictionary<string, object>
                {
                    ["serverFqdn"] = "fmis-dev-persistence-postgres.postgres.database.azure.com",
                    ["databaseName"] = "fmis",
                    ["appIdentityClientId"] = "00000000-0000-0000-0000-000000000001",
                    ["appIdentityPrincipalId"] = "00000000-0000-0000-0000-000000000002",
                    ["appIdentityName"] = "fmis-dev-app-identity",
                };
            var state = args.Inputs.ToBuilder();
            state["outputs"] = outputs;
            return Task.FromResult<(string?, object)>(($"{args.Name}_id", state.ToImmutable()));
        }
```
Verify the exact StackReference type token + the output property name (`outputs`) against the installed Pulumi runtime; adjust if the mock surfaces them differently (e.g. via `CallAsync`). The reference `Name` the component passes (Task B3+) must contain `auth`/`persistence` so this branch selects correctly.

- [ ] **Step 2: Add `RunApplicationStackAsync` to `InfraTesting`** (mirror `RunPersistenceStackAsync` — same `StackMocks`, `TestOptions { StackName="dev", ProjectName="fmis-application", IsPreview=false }`, env restore in `finally` for any env the stack reads). No live token seam is needed here.

- [ ] **Step 3: Build** → `dotnet build Fmis.slnx` succeeds. **Commit.**

## Task B3: ContainerRegistry component

**Files:** Create `infra/application/Components/ContainerRegistry.cs`, `infra/tests/Fmis.Infra.Tests/ApplicationStackTests.cs`; Modify `infra/application/ApplicationStack.cs`.

**Interfaces:** Produces `ContainerRegistry` exposing `Output<string> LoginServer` and `AzureNative.ContainerRegistry.Registry Registry`.

- [ ] **Step 1: Failing test** — `ApplicationStackTests.cs`:
```csharp
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Tests;

public class ApplicationStackTests
{
    [Fact]
    public async Task Creates_a_basic_container_registry()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var registry = resources.OfType<AzureNative.ContainerRegistry.Registry>().Single();
        Assert.Equal("Basic", await InfraTesting.GetAsync(registry.Sku.Apply(s => s.Name!)));
    }
}
```
Verify `Registry`/`Sku` member shapes against AzureNative; adjust the `Sku.Name` access if the output type differs.

- [ ] **Step 2: Run red** → `dotnet test Fmis.slnx --filter "FullyQualifiedName~ApplicationStackTests"` → FAIL.

- [ ] **Step 3: Implement** the component + resource group + compose. `infra/application/Components/ContainerRegistry.cs`:
```csharp
using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Application.Components;

public sealed class ContainerRegistry : ComponentResource
{
    public Output<string> LoginServer { get; }
    public AzureNative.ContainerRegistry.Registry Registry { get; }

    public ContainerRegistry(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:application:ContainerRegistry", name, options)
    {
        Registry = new AzureNative.ContainerRegistry.Registry(name, new AzureNative.ContainerRegistry.RegistryArgs
        {
            ResourceGroupName = resourceGroupName,
            RegistryName = name,
            Location = location,
            Sku = new AzureNative.ContainerRegistry.Inputs.SkuArgs { Name = AzureNative.ContainerRegistry.SkuName.Basic },
            AdminUserEnabled = false,
        }, new CustomResourceOptions { Parent = this });

        LoginServer = Registry.LoginServer;
        RegisterOutputs();
    }
}
```
In `ApplicationStack.cs`, read `env`, create the resource group, and the registry (compacted alphanumeric name `fmis{env}acr`):
```csharp
        var env = Deployment.Instance.StackName;
        const string location = "centralus";
        var resourceGroup = new AzureNative.Resources.ResourceGroup(ResourceNames.For(env, "application", "rg"),
            new AzureNative.Resources.ResourceGroupArgs { ResourceGroupName = ResourceNames.For(env, "application", "rg"), Location = location });
        var registry = new ContainerRegistry($"fmis{env}acr", resourceGroup.Name, location);
```
Verify `SkuName.Basic`/`LoginServer` names. Add a `StackMocks` name mapping for `registryName → name` if a later test asserts the registry `.Name` (this test asserts `Sku`, so it may be unneeded).

- [ ] **Step 4: Run green** (filter + full `~Fmis.Infra`). **Commit.**

## Task B4: BackendApp — environment, app, identity, ingress, scale, env vars

**Files:** Create `infra/application/Components/BackendApp.cs`; Modify `infra/application/ApplicationStack.cs`, `ApplicationStackTests.cs`.

**Interfaces:** Consumes the ACR (`LoginServer`, `Registry`), the pushed image ref (Task B5 — for now a placeholder string input), and the auth + persistence stack-reference outputs. Produces `BackendApp` exposing `Output<string> Url` (the ingress FQDN, `https://…`).

- [ ] **Step 1: Failing tests** — add to `ApplicationStackTests`:
```csharp
[Fact]
public async Task Runs_an_externally_ingressed_scale_to_zero_backend_app()
{
    var resources = await InfraTesting.RunApplicationStackAsync();

    var app = resources.OfType<AzureNative.App.ContainerApp>().Single();
    Assert.Equal(8080, await InfraTesting.GetAsync(app.Configuration.Apply(c => c!.Ingress!.TargetPort!.Value)));
    Assert.Equal(0, await InfraTesting.GetAsync(app.Template.Apply(t => t!.Scale!.MinReplicas!.Value)));
}

[Fact]
public async Task Injects_db_auth0_and_cors_settings_into_the_backend()
{
    var resources = await InfraTesting.RunApplicationStackAsync();

    var app = resources.OfType<AzureNative.App.ContainerApp>().Single();
    var envVars = await InfraTesting.GetAsync(app.Template.Apply(t =>
        t!.Containers![0].Env!.ToDictionary(e => e.Name!, e => e.Value)));
    Assert.Equal("true", envVars["Database__UseEntraAuth"]);
    Assert.Contains("fmis-dev-persistence-postgres.postgres.database.azure.com", envVars["ConnectionStrings__Fmis"]);
    Assert.Contains("Username=fmis-dev-app-identity", envVars["ConnectionStrings__Fmis"]);
    Assert.DoesNotContain("Password=", envVars["ConnectionStrings__Fmis"]);
    Assert.Equal("https://fmis-dev.us.auth0.com/", envVars["Auth0__Authority"]);
    Assert.Equal("https://dev.api.modern-fmis", envVars["Auth0__Audience"]);
    Assert.Equal("00000000-0000-0000-0000-000000000001", envVars["AZURE_CLIENT_ID"]);
}
```
Verify `ContainerApp`/`Configuration`/`Ingress`/`Template`/`Scale`/`Containers`/`Env` member + nullability shapes against AzureNative `App` and adjust the `.Apply(...)` chains.

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** `BackendApp` (sealed component). Create a `ManagedEnvironment` then a `ContainerApp` with: `ManagedEnvironmentId`, an `Identity` of type `UserAssigned` referencing the persistence identity's resource id, `Configuration` with external `Ingress { External=true, TargetPort=8080 }` and a `Registry` entry (`Server=acrLoginServer`, `Identity=<userAssignedIdentityId>`), `Template` with one `Container { Image=imageRef, Name="backend", Env=[…] }` and `Scale { MinReplicas=0, MaxReplicas=2 }`. The env list is built from the stack-reference outputs:
```csharp
// constructor inputs: Input<string> resourceGroupName, string location, Input<string> imageRef,
//   Input<string> acrLoginServer, Input<string> identityResourceId, Input<string> identityClientId,
//   Input<string> serverFqdn, Input<string> databaseName, Input<string> identityName,
//   Input<string> authDomain, Input<string> audience, Input<string> frontendUrl
var connectionString = Output.Format($"Host={serverFqdn};Database={databaseName};Username={identityName};Ssl Mode=Require");
var authority = Output.Format($"https://{authDomain}/");
// Env entries (AzureNative.App.Inputs.EnvironmentVarArgs): ConnectionStrings__Fmis=connectionString,
//   Database__UseEntraAuth="true", AZURE_CLIENT_ID=identityClientId, Auth0__Authority=authority,
//   Auth0__Audience=audience, Cors__AllowedOrigin=frontendUrl
Url = app.Configuration.Apply(c => $"https://{c!.Ingress!.Fqdn}");
```
Also create the **`RoleAssignment`** granting the identity's `principalId` the **AcrPull** role on the registry scope (`AzureNative.Authorization.RoleAssignment`, `RoleDefinitionId` = the AcrPull built-in role id `…/7f951dda-4ed3-4680-a7ca-43fe172d538d`, `PrincipalId`=identityPrincipalId, `PrincipalType=ServicePrincipal`, `Scope`=registry.Id). In `ApplicationStack`, read the stack references and the persistence/auth outputs, and pass them in (use a placeholder image ref like `"mcr.microsoft.com/azuredocs/aci-helloworld:latest"` until Task B5 supplies the real one):
```csharp
var auth = new StackReference("auth", new StackReferenceArgs { Name = $"organization/fmis-auth/{env}" });
var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"organization/fmis-persistence/{env}" });
```
Verify the StackReference name format for the azblob backend (it may be just `fmis-auth/dev` without an org segment — confirm against how the existing stacks are named/logged; the mock in B2 only needs the name to contain `auth`/`persistence`). Read outputs with `auth.GetOutput("domain")` etc. Verify all AzureNative `App` arg/enum names.

- [ ] **Step 4: Run green** (filter + full infra). **Commit.**

## Task B5: Pulumi-built backend image

**Files:** Modify `infra/application/ApplicationStack.cs`, `infra/application/Components/BackendApp.cs` (image ref input), `ApplicationStackTests.cs`.

- [ ] **Step 1: Failing test** — assert an image-build resource exists and the app uses the registry image:
```csharp
[Fact]
public async Task Builds_and_references_the_backend_image_from_the_registry()
{
    var resources = await InfraTesting.RunApplicationStackAsync();

    Assert.NotEmpty(resources.OfType<Pulumi.DockerBuild.Image>());
    var app = resources.OfType<AzureNative.App.ContainerApp>().Single();
    var image = await InfraTesting.GetAsync(app.Template.Apply(t => t!.Containers![0].Image!));
    Assert.Contains("fmis", image);
}
```
Verify the docker-build image type (`Pulumi.DockerBuild.Image`, else `Pulumi.Docker.Image`) against the installed package.

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** — in `ApplicationStack`, create the image build resource pushing to the ACR, ordered after the registry, and pass its ref to `BackendApp`:
```csharp
var image = new Pulumi.DockerBuild.Image($"fmis{env}acr-backend", new Pulumi.DockerBuild.ImageArgs
{
    Context = new Pulumi.DockerBuild.Inputs.BuildContextArgs { Location = ".." /* repo root, relative to the stack cwd; verify */ },
    Dockerfile = new Pulumi.DockerBuild.Inputs.DockerfileArgs { Location = "../backend/src/Fmis.Api/Dockerfile" },
    Tags = { Output.Format($"{registry.LoginServer}/fmis-backend:latest") },
    Push = true,
    Registries = { /* registry login server + identity-based or admin creds — verify auth model */ },
}, new CustomResourceOptions { DependsOn = { registry.Registry } });
```
Push auth to ACR during `pulumi up`: the runner is `az login`'d (CD) — verify whether `DockerBuild.Image` can authenticate to ACR via the ambient Azure CLI/token or needs explicit `Registries` credentials (e.g. an `az acr login` step in CD, or ACR admin creds). Document the chosen auth path. Use the repo-root build context (the post-consolidation context that the Dockerfile expects). Pass `image.Ref` (the pushed digest/tag) as the `imageRef` to `BackendApp` (replacing the placeholder).

- [ ] **Step 4: Run green** (filter + full infra). **Commit.**

## Task B6: FrontendSite — static website + assets + config.json

**Files:** Create `infra/application/Components/FrontendSite.cs`; Modify `infra/application/ApplicationStack.cs`, `ApplicationStackTests.cs`.

**Interfaces:** Consumes `backendUrl`, the auth outputs (domain/spaClientId/audience). Produces `FrontendSite` exposing `Output<string> Url` (the static-website endpoint).

- [ ] **Step 1: Failing tests**:
```csharp
[Fact]
public async Task Hosts_a_static_website_storage_account()
{
    var resources = await InfraTesting.RunApplicationStackAsync();
    Assert.NotEmpty(resources.OfType<AzureNative.Storage.StorageAccount>());
    Assert.NotEmpty(resources.OfType<AzureNative.Storage.StorageAccountStaticWebsite>());
}

[Fact]
public async Task Writes_a_config_json_blob_with_the_spa_settings()
{
    var resources = await InfraTesting.RunApplicationStackAsync();

    Assert.NotEmpty(resources.OfType<AzureNative.Storage.Blob>());
    var site = resources.OfType<Fmis.Infra.Application.Components.FrontendSite>().Single();
    var json = await InfraTesting.GetAsync(site.ConfigJson);
    Assert.Contains("\"apiBaseUrl\"", json);
    Assert.Contains("\"audience\"", json);
}
```
`FrontendSite` exposes the serialized config as `public Output<string> ConfigJson { get; }` (the same value written to the blob `Source`), so the content is asserted directly without reading the blob's `AssetOrArchive` through the mock. Verify `StorageAccountStaticWebsite`/`Blob`/`Source`/`PrimaryEndpoints` shapes against AzureNative Storage and adjust.

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** `FrontendSite`: a `StorageAccount` (Standard_LRS, StorageV2), a `StorageAccountStaticWebsite` (`IndexDocument="index.html"`, `Error404Document="index.html"` for SPA fallback), upload `frontend/dist` to the `$web` container (synced-folder, or per-file `Blob` resources over the built files), and a `config.json` `Blob` in `$web` whose content is the serialized config:
```csharp
var config = Output.Format($"{{\"apiBaseUrl\":\"{backendUrl}\",\"auth\":{{\"domain\":\"{authDomain}\",\"clientId\":\"{spaClientId}\",\"audience\":\"{audience}\"}}}}");
var configBlob = new AzureNative.Storage.Blob("config.json", new AzureNative.Storage.BlobArgs
{
    ResourceGroupName = resourceGroupName,
    AccountName = account.Name,
    ContainerName = "$web",
    BlobName = "config.json",
    ContentType = "application/json",
    Source = config.Apply(c => (AssetOrArchive)new StringAsset(c)),
}, new CustomResourceOptions { Parent = this });
Url = account.PrimaryEndpoints.Apply(e => e.Web);
```
Verify the static-website resource + the `$web` container handling + `PrimaryEndpoints.Web` against AzureNative Storage. For the `dist/` upload, prefer `Pulumi.SyncedFolder` (`AzureBlobFolder` → `$web`); if unavailable, glob `frontend/dist` into per-file `Blob` resources. Compose `FrontendSite` in `ApplicationStack` (account name `fmis{env}web`), then wire `BackendApp`'s `Cors__AllowedOrigin` to `frontend.Url` (it's independently derivable — no cycle).

- [ ] **Step 4: Run green** (filter + full infra). **Commit.**

## Task B7: Stack outputs + dev config

**Files:** Modify `infra/application/ApplicationStack.cs`; Create `infra/application/Pulumi.dev.yaml`; Modify `ApplicationStackTests.cs`.

- [ ] **Step 1: Failing test**:
```csharp
[Fact]
public async Task Exposes_backend_and_frontend_urls()
{
    var resources = await InfraTesting.RunApplicationStackAsync();
    var stack = resources.OfType<Fmis.Infra.Application.ApplicationStack>().Single();

    Assert.NotNull(await InfraTesting.GetAsync(stack.BackendUrl));
    Assert.NotNull(await InfraTesting.GetAsync(stack.FrontendUrl));
}
```

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** — add `[Output("backendUrl")] public Output<string> BackendUrl { get; private set; }` and `[Output("frontendUrl")] public Output<string> FrontendUrl { get; private set; }`, assigned from `backendApp.Url` / `frontendSite.Url`. Create `Pulumi.dev.yaml` with `config: {}` (no committed secrets; stack references + the OIDC/Azure context supply everything).

- [ ] **Step 4: Run green** → full `dotnet test Fmis.slnx` (backend + all infra) green. **Commit.**

## Task B8: CD — the real apps job

**Files:** Modify `.github/workflows/cd.yml`.

- [ ] **Step 1: Replace the placeholder `apps` job** with a real deploy job:
```yaml
  apps:
    needs: [infra]
    runs-on: ubuntu-latest
    environment: dev
    env:
      PULUMI_STATE_ACCOUNT: fmisdevtfstate
      PULUMI_STATE_CONTAINER: pulumi-state
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json
      - uses: actions/setup-node@v6
        with:
          node-version: 24
      - run: corepack enable
      - name: Build frontend
        working-directory: frontend
        run: pnpm install --frozen-lockfile && pnpm build
      - uses: azure/login@v3
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - uses: pulumi/actions@v7
        with:
          pulumi-version: 3.247.0
      - name: Pulumi login (azblob)
        run: pulumi login "azblob://${PULUMI_STATE_CONTAINER}?storage_account=${PULUMI_STATE_ACCOUNT}"
      - name: Select or init the application stack
        working-directory: infra/application
        run: pulumi stack select dev || pulumi stack init dev
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
      - name: Application up
        working-directory: infra/application
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
        run: pulumi up --yes
```
The `apps` job runs only on the CD path (it `needs: [infra]`, which is gated by the `workflow_run` success+push `if`). If the Pulumi image build needs ACR auth, add an `az acr login --name fmis${ENVIRONMENT}acr` step after `azure/login` (resolve per Task B5's chosen auth path). Keep the existing `infra` job unchanged.

- [ ] **Step 2: Validate** → `ruby -ryaml -e 'YAML.load_file(".github/workflows/cd.yml"); puts :ok'`. **Commit.**

---

## Done criteria

- `dotnet build Fmis.slnx` clean; `dotnet test Fmis.slnx` green (backend incl. the new data-source + CORS tests, all infra incl. the application Pulumi.Testing suite); the contract test + `docker compose` unchanged.
- The application stack models: ACR (Basic), a Container Apps environment + backend app (assigned `fmis-dev-app-identity`, `AcrPull`, external ingress 8080, `minReplicas: 0`, env vars from the auth/persistence stack references incl. the password-less connection string + `Database__UseEntraAuth` + `AZURE_CLIENT_ID` + Auth0 + CORS), a Pulumi-built backend image pushed to ACR, and a Storage static website with the uploaded `dist/` + a `config.json` blob (`apiBaseUrl` + auth domain/clientId/audience), plus `backendUrl`/`frontendUrl` outputs.
- CD's `apps` job builds the frontend and runs `pulumi up` on `infra/application`.
- **Deferred (3c):** live login + Playwright; `CREATE EXTENSION postgis` (Field phase).
```
