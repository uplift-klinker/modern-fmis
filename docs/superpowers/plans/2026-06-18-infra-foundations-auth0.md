# Infrastructure Foundations + Auth0 (Phase 3a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the Pulumi (C#) infrastructure foundation and the Auth0 `auth` stack for a single `dev` environment — CI-driven minus a documented manual bootstrap — emitting real Auth0 values (`domain`, SPA `clientId`, `audience`) plus conditional E2E creds.

**Architecture:** A new top-level `infra/` .NET solution holds a shared `Fmis.Infra.Common` library (resource-naming helper), a per-layer Pulumi program `auth/` (`fmis-auth` project, `dev` stack) provisioning Auth0 resources, and `Fmis.Infra.Tests` unit-testing the stack with the `Pulumi.Testing` mock framework. Self-managed Pulumi state lives in an `azblob` storage account bootstrapped by an idempotent `az` CLI script. GitHub Actions runs `pulumi preview` on PRs and `pulumi up` on merge, authenticating to Azure via OIDC.

**Tech Stack:** .NET 10 (pinned via `infra/global.json`), Pulumi C# SDK + `Pulumi.Auth0` + `Pulumi.Random`, `Pulumi.Testing` + xUnit, Azure (`az` CLI), GitHub Actions.

---

## Provider/SDK API note (read first)

Pulumi provider resource shapes evolve across versions. The C# code in this plan targets the current `Pulumi.Auth0` / `Pulumi.Random` / `Pulumi` SDKs, but **the implementer must verify exact property names and resource splits against the pinned package versions** (e.g. some Auth0 provider versions move client auth settings to a separate `ClientCredentials` resource). When a build error reveals a renamed/moved property, adjust to the installed SDK — that is expected for IaC, not a deviation from the plan. Pin every package to its resolved version in the `.csproj` (repo convention: exact versions, no floating ranges).

Run all `dotnet` commands from `infra/` (a dedicated `infra/global.json` pins the SDK). Run all shell commands via `zsh -lc '…'`.

---

## File structure

```
infra/
  global.json                         # pins .NET 10.0.102 (mirrors backend)
  Fmis.Infra.slnx                     # solution tying the projects together
  Directory.Build.props               # shared Nullable/ImplicitUsings/LangVersion
  Fmis.Infra.Common/
    Fmis.Infra.Common.csproj          # class library (no Pulumi dependency needed for naming)
    ResourceNames.cs                  # naming helper: For(env,layer,resource), Audience(env)
  auth/
    Fmis.Infra.Auth.csproj            # Pulumi program; refs Common + Pulumi + Pulumi.Auth0 + Pulumi.Random
    Pulumi.yaml                       # name: fmis-auth, runtime: dotnet
    Pulumi.dev.yaml                   # dev stack config (enableE2eUser; auth0:* injected by CI)
    Program.cs                        # Deployment.RunAsync<AuthStack>
    AuthStack.cs                      # the stack: SPA app, API, tenant default_directory, conditional e2e, outputs
  tests/
    Fmis.Infra.Tests/
      Fmis.Infra.Tests.csproj         # xUnit + ref to Common and auth project
      InfraTesting.cs                 # Pulumi.Testing harness: IMocks + run + output extraction
      ResourceNamesTests.cs           # naming helper tests
      AuthStackTests.cs               # stack tests (SPA/API/default_directory/conditional e2e/outputs)
  scripts/
    bootstrap-state.sh                # idempotent az CLI: resource group + azblob storage account + container
.github/workflows/
  infra.yml                           # PR: build/test + preview ; merge: up (auth.dev)
docs/
  auth0-tenant-setup.md               # manual bootstrap runbook (initial + additional tenant)
```

Each later layer (3b `persistence`, `application`) is added as a sibling Pulumi project under `infra/` with the same shape, reusing `Fmis.Infra.Common`.

---

## Task 1: Solution scaffold + naming helper (TDD)

**Files:**
- Create: `infra/global.json`, `infra/Directory.Build.props`, `infra/Fmis.Infra.Common/Fmis.Infra.Common.csproj`, `infra/Fmis.Infra.Common/ResourceNames.cs`, `infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj`, `infra/tests/Fmis.Infra.Tests/ResourceNamesTests.cs`, `infra/Fmis.Infra.slnx`

- [ ] **Step 1: Scaffold the solution and projects**

```bash
cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra
cat > global.json <<'JSON'
{
  "sdk": {
    "version": "10.0.102",
    "rollForward": "latestFeature"
  }
}
JSON
cat > Directory.Build.props <<'XML'
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
XML
zsh -lc 'dotnet new classlib -n Fmis.Infra.Common -o Fmis.Infra.Common'
zsh -lc 'dotnet new xunit -n Fmis.Infra.Tests -o tests/Fmis.Infra.Tests'
rm -f Fmis.Infra.Common/Class1.cs tests/Fmis.Infra.Tests/UnitTest1.cs
zsh -lc 'dotnet new sln -n Fmis.Infra --format slnx'
zsh -lc 'dotnet sln Fmis.Infra.slnx add Fmis.Infra.Common/Fmis.Infra.Common.csproj tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj'
zsh -lc 'dotnet add tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj reference Fmis.Infra.Common/Fmis.Infra.Common.csproj'
```

If `dotnet new sln --format slnx` is unavailable, create `Fmis.Infra.sln` instead and use that path everywhere. Pin the xUnit test template's package versions in `tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj` to match the backend: `xunit` `2.9.3`, `xunit.runner.visualstudio` `3.1.4`, `Microsoft.NET.Test.Sdk` `17.14.1`, `coverlet.collector` `6.0.4`. Add `<Using Include="Xunit" />` to that csproj's ItemGroup.

- [ ] **Step 2: Write the failing test**

`infra/tests/Fmis.Infra.Tests/ResourceNamesTests.cs`:

```csharp
using Fmis.Infra.Common;

namespace Fmis.Infra.Tests;

public class ResourceNamesTests
{
    [Fact]
    public void For_builds_a_prefixed_env_layer_resource_name()
    {
        var name = ResourceNames.For("dev", "auth", "spa");

        Assert.Equal("fmis-dev-auth-spa", name);
    }

    [Fact]
    public void Audience_is_environment_first()
    {
        var audience = ResourceNames.Audience("dev");

        Assert.Equal("https://dev.api.modern-fmis", audience);
    }
}
```

- [ ] **Step 3: Run it (red)**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx'`
Expected: FAIL — `ResourceNames` does not exist (compile error).

- [ ] **Step 4: Implement the naming helper**

`infra/Fmis.Infra.Common/ResourceNames.cs`:

```csharp
namespace Fmis.Infra.Common;

public static class ResourceNames
{
    public static string For(string environment, string layer, string resource)
        => $"fmis-{environment}-{layer}-{resource}";

    public static string Audience(string environment)
        => $"https://{environment}.api.modern-fmis";
}
```

> The compacted form for constrained Azure resources (storage accounts) is **not** built here — no Pulumi-managed constrained resource exists in 3a (the state storage account is named by `bootstrap-state.sh`). It is added to this helper in 3b when the first such resource appears (YAGNI).

- [ ] **Step 5: Run it (green)**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx'`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add infra && git commit -m "Scaffold infra solution + resource-naming helper (TDD)"
```

---

## Task 2: Auth Pulumi project + testing harness + SPA application (TDD)

**Files:**
- Create: `infra/auth/Fmis.Infra.Auth.csproj`, `infra/auth/Pulumi.yaml`, `infra/auth/AuthStack.cs`, `infra/auth/Program.cs`, `infra/tests/Fmis.Infra.Tests/InfraTesting.cs`, `infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`
- Modify: `infra/Fmis.Infra.slnx`, `infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj`

- [ ] **Step 1: Scaffold the auth Pulumi project**

```bash
cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra
mkdir -p auth
zsh -lc 'cd auth && dotnet new console -n Fmis.Infra.Auth -o . && rm -f Program.cs'
zsh -lc 'cd auth && dotnet add Fmis.Infra.Auth.csproj package Pulumi'
zsh -lc 'cd auth && dotnet add Fmis.Infra.Auth.csproj package Pulumi.Auth0'
zsh -lc 'cd auth && dotnet add Fmis.Infra.Auth.csproj package Pulumi.Random'
zsh -lc 'cd auth && dotnet add Fmis.Infra.Auth.csproj reference ../Fmis.Infra.Common/Fmis.Infra.Common.csproj'
zsh -lc 'dotnet sln Fmis.Infra.slnx add auth/Fmis.Infra.Auth.csproj'
zsh -lc 'dotnet add tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj reference auth/Fmis.Infra.Auth.csproj'
```

Pin the resolved `Pulumi`, `Pulumi.Auth0`, `Pulumi.Random` versions in `Fmis.Infra.Auth.csproj` (exact versions, repo convention).

`infra/auth/Pulumi.yaml`:

```yaml
name: fmis-auth
runtime: dotnet
description: Auth0 resources for modern-fmis (authentication only)
```

- [ ] **Step 2: Add the empty stack and the Pulumi.Testing harness**

`infra/auth/AuthStack.cs`:

```csharp
using Pulumi;

namespace Fmis.Infra.Auth;

public class AuthStack : Stack
{
    public AuthStack()
    {
    }
}
```

`infra/tests/Fmis.Infra.Tests/InfraTesting.cs`:

```csharp
using System.Collections.Immutable;
using Pulumi;
using Pulumi.Testing;

namespace Fmis.Infra.Tests;

internal sealed class StackMocks : IMocks
{
    public Task<(string? Id, object State)> NewResourceAsync(MockResourceArgs args)
        => Task.FromResult<(string?, object)>(($"{args.Name}_id", args.Inputs));

    public Task<object> CallAsync(MockCallArgs args)
        => Task.FromResult<object>(args.Args);
}

internal static class InfraTesting
{
    public static Task<ImmutableArray<Resource>> RunAuthStackAsync(bool enableE2eUser)
    {
        // The stack reads auth0:* (provider config + the `domain` output) and fmis-auth:enableE2eUser.
        // Provide all of them so the stack constructs under mocks (no real Auth0 calls happen).
        Environment.SetEnvironmentVariable(
            "PULUMI_CONFIG",
            $$"""{"fmis-auth:enableE2eUser":"{{(enableE2eUser ? "true" : "false")}}","auth0:domain":"dev.modern-fmis.auth0.com","auth0:clientId":"test-client-id","auth0:clientSecret":"test-client-secret"}""");

        return Deployment.TestAsync<Fmis.Infra.Auth.AuthStack>(
            new StackMocks(),
            new TestOptions { StackName = "dev", ProjectName = "fmis-auth", IsPreview = false });
    }

    public static Task<T> GetAsync<T>(Output<T> output)
    {
        var completion = new TaskCompletionSource<T>();
        output.Apply(value =>
        {
            completion.SetResult(value);
            return value;
        });
        return completion.Task;
    }
}
```

> If the installed `Pulumi.Testing` does not pick up config via the `PULUMI_CONFIG` environment variable, set it through the available `TestOptions`/config mechanism for that SDK version; the contract this harness must satisfy is "run `AuthStack` for stack `dev` with `enableE2eUser` on or off and return the registered resources."

- [ ] **Step 3: Write the failing test (SPA application)**

`infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`:

```csharp
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Tests;

public class AuthStackTests
{
    [Fact]
    public async Task Creates_the_spa_application_named_for_the_environment()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        var clients = resources.OfType<Auth0.Client>().ToList();
        var spa = clients.Single(c => InfraTesting.GetAsync(c.AppType).Result == "spa");
        Assert.Equal("fmis-dev-auth-spa", await InfraTesting.GetAsync(spa.Name));
    }
}
```

- [ ] **Step 4: Run it (red)**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx --filter FullyQualifiedName~AuthStackTests'`
Expected: FAIL — no `Auth0.Client` registered (`Single` throws).

- [ ] **Step 5: Implement the SPA application**

`infra/auth/AuthStack.cs`:

```csharp
using Pulumi;
using Auth0 = Pulumi.Auth0;
using Fmis.Infra.Common;

namespace Fmis.Infra.Auth;

public class AuthStack : Stack
{
    public AuthStack()
    {
        var env = Deployment.Instance.StackName;

        var spaName = ResourceNames.For(env, "auth", "spa");
        var spa = new Auth0.Client(spaName, new Auth0.ClientArgs
        {
            Name = spaName,
            AppType = "spa",
            OidcConformant = true,
            GrantTypes = { "authorization_code", "refresh_token" },
            Callbacks = { "http://localhost:5173" },
            AllowedLogoutUrls = { "http://localhost:5173" },
            WebOrigins = { "http://localhost:5173" },
        });
    }
}
```

- [ ] **Step 6: Run it (green) + commit**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx'`
Expected: PASS.

```bash
git add infra && git commit -m "Add auth Pulumi project + Pulumi.Testing harness + SPA application (TDD)"
```

---

## Task 3: API / resource server with env-named audience (TDD)

**Files:**
- Modify: `infra/auth/AuthStack.cs`, `infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `AuthStackTests`:

```csharp
[Fact]
public async Task Creates_the_api_resource_server_with_the_env_named_audience()
{
    var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

    var api = resources.OfType<Auth0.ResourceServer>().Single();
    Assert.Equal("fmis-dev-auth-api", await InfraTesting.GetAsync(api.Name));
    Assert.Equal("https://dev.api.modern-fmis", await InfraTesting.GetAsync(api.Identifier));
}
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx --filter FullyQualifiedName~AuthStackTests'`
Expected: FAIL — no `Auth0.ResourceServer` registered.

- [ ] **Step 3: Implement the resource server**

Add to `AuthStack` constructor (after the SPA client):

```csharp
        var apiName = ResourceNames.For(env, "auth", "api");
        var api = new Auth0.ResourceServer(apiName, new Auth0.ResourceServerArgs
        {
            Name = apiName,
            Identifier = ResourceNames.Audience(env),
            SigningAlg = "RS256",
        });
```

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx'`
Expected: PASS.

```bash
git add infra && git commit -m "Add Auth0 API resource server with env-named audience (TDD)"
```

---

## Task 4: Tenant default_directory (TDD)

**Files:**
- Modify: `infra/auth/AuthStack.cs`, `infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `AuthStackTests`:

```csharp
[Fact]
public async Task Sets_the_tenant_default_directory_to_the_database_connection()
{
    var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

    var tenant = resources.OfType<Auth0.Tenant>().Single();
    Assert.Equal("Username-Password-Authentication", await InfraTesting.GetAsync(tenant.DefaultDirectory));
}
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx --filter FullyQualifiedName~AuthStackTests'`
Expected: FAIL — no `Auth0.Tenant` registered.

- [ ] **Step 3: Implement the tenant setting**

Add to `AuthStack` constructor:

```csharp
        var tenant = new Auth0.Tenant(ResourceNames.For(env, "auth", "tenant"), new Auth0.TenantArgs
        {
            DefaultDirectory = "Username-Password-Authentication",
        });
```

> `Username-Password-Authentication` is the default database connection name created with every new Auth0 tenant. The runbook (Task 10) notes that the bootstrap must not rename/delete it.

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx'`
Expected: PASS.

```bash
git add infra && git commit -m "Manage Auth0 tenant default_directory for ROPG (TDD)"
```

---

## Task 5: Conditional E2E test user + password-grant client (TDD)

**Files:**
- Modify: `infra/auth/AuthStack.cs`, `infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`

- [ ] **Step 1: Write the failing tests (present when enabled, absent when off)**

Add to `AuthStackTests`:

```csharp
[Fact]
public async Task Provisions_the_e2e_user_and_client_when_enabled()
{
    var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: true);

    var user = resources.OfType<Auth0.User>().Single();
    Assert.Equal("Username-Password-Authentication", await InfraTesting.GetAsync(user.ConnectionName));

    var e2eClient = resources.OfType<Auth0.Client>()
        .Single(c => InfraTesting.GetAsync(c.Name).Result == "fmis-dev-auth-e2e");
    var grantTypes = await InfraTesting.GetAsync(e2eClient.GrantTypes);
    Assert.Contains("password", grantTypes);
}

[Fact]
public async Task Omits_the_e2e_user_and_client_when_disabled()
{
    var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

    Assert.Empty(resources.OfType<Auth0.User>());
    Assert.DoesNotContain(
        resources.OfType<Auth0.Client>(),
        c => InfraTesting.GetAsync(c.Name).Result == "fmis-dev-auth-e2e");
}
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx --filter FullyQualifiedName~AuthStackTests'`
Expected: FAIL — no `Auth0.User`/e2e client when enabled.

- [ ] **Step 3: Implement the conditional E2E resources**

Add `using Random = Pulumi.Random;` to the top of `AuthStack.cs`, then add to the constructor (after the API/tenant):

```csharp
        var config = new Config();
        if (config.GetBoolean("enableE2eUser") ?? false)
        {
            var password = new Random.RandomPassword(ResourceNames.For(env, "auth", "e2e-password"),
                new Random.RandomPasswordArgs { Length = 24, Special = true });

            var user = new Auth0.User(ResourceNames.For(env, "auth", "e2e-user"), new Auth0.UserArgs
            {
                ConnectionName = "Username-Password-Authentication",
                Email = "e2e@dev.modern-fmis.test",
                EmailVerified = true,
                Password = password.Result,
            });

            var e2eName = ResourceNames.For(env, "auth", "e2e");
            var e2eClient = new Auth0.Client(e2eName, new Auth0.ClientArgs
            {
                Name = e2eName,
                AppType = "non_interactive",
                OidcConformant = true,
                GrantTypes = { "password", "http://auth0.com/oauth/grant-type/password-realm" },
            });
        }
```

> `new Config()` reads the project-scoped config (`fmis-auth:enableE2eUser`). The password-realm grant type is Auth0's realm-aware ROPG variant; keep both so the e2e job can target the database connection realm. Verify the exact e2e client `AppType`/grant constants against the installed `Pulumi.Auth0` version.

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx'`
Expected: PASS.

```bash
git add infra && git commit -m "Conditionally provision Auth0 e2e user + password-grant client (TDD)"
```

---

## Task 6: Stack outputs (TDD)

**Files:**
- Modify: `infra/auth/AuthStack.cs`, `infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `AuthStackTests`:

```csharp
[Fact]
public async Task Exposes_domain_spa_client_id_and_audience_outputs()
{
    var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);
    var stack = resources.OfType<Fmis.Infra.Auth.AuthStack>().Single();

    Assert.Equal("https://dev.api.modern-fmis", await InfraTesting.GetAsync(stack.Audience));
    Assert.NotNull(await InfraTesting.GetAsync(stack.SpaClientId));
}
```

- [ ] **Step 2: Run it (red)**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx --filter FullyQualifiedName~AuthStackTests'`
Expected: FAIL — `AuthStack` has no `Audience`/`SpaClientId` members.

- [ ] **Step 3: Implement the outputs**

Add `[Output]` properties to `AuthStack` and assign them. The `domain` comes from the Auth0 provider config (`auth0:domain`). Update the class:

```csharp
public class AuthStack : Stack
{
    [Output("domain")] public Output<string> Domain { get; private set; }
    [Output("spaClientId")] public Output<string> SpaClientId { get; private set; }
    [Output("audience")] public Output<string> Audience { get; private set; }
    [Output("e2eClientId")] public Output<string?> E2eClientId { get; private set; }
    [Output("e2eClientSecret")] public Output<string?> E2eClientSecret { get; private set; }
    [Output("e2eUsername")] public Output<string?> E2eUsername { get; private set; }
    [Output("e2ePassword")] public Output<string?> E2ePassword { get; private set; }

    public AuthStack()
    {
        var env = Deployment.Instance.StackName;
        var auth0Config = new Config("auth0");

        // ... SPA client (spa), API (api), tenant as before ...

        Domain = Output.Create(auth0Config.Require("domain"));
        SpaClientId = spa.ClientId;
        Audience = api.Identifier;

        E2eClientId = Output.Create((string?)null);
        E2eClientSecret = Output.CreateSecret((string?)null);
        E2eUsername = Output.CreateSecret((string?)null);
        E2ePassword = Output.CreateSecret((string?)null);

        var config = new Config();
        if (config.GetBoolean("enableE2eUser") ?? false)
        {
            // ... password, user, e2eClient as in Task 5 ...
            E2eClientId = e2eClient.ClientId!;
            E2eClientSecret = Output.CreateSecret(e2eClient.ClientSecret!);
            E2eUsername = Output.CreateSecret(user.Email!);
            E2ePassword = Output.CreateSecret(password.Result);
        }
    }
}
```

> `spa.ClientId` and `e2eClient.ClientId`/`ClientSecret` are provider outputs; verify their exact member names. Keep the e2e outputs declared (null) in the disabled case so the output schema is stable; only the enabled case populates them, and the sensitive ones are wrapped in `Output.CreateSecret`.

- [ ] **Step 4: Run it (green) + commit**

Run: `zsh -lc 'cd infra && dotnet test Fmis.Infra.slnx'`
Expected: PASS (all `AuthStackTests` + `ResourceNamesTests`).

```bash
git add infra && git commit -m "Expose auth stack outputs (domain/spaClientId/audience + secret e2e creds) (TDD)"
```

---

## Task 7: Program entry point + stack config

**Files:**
- Create: `infra/auth/Program.cs`, `infra/auth/Pulumi.dev.yaml`

- [ ] **Step 1: Add the program entry point**

`infra/auth/Program.cs`:

```csharp
using Pulumi;

return await Deployment.RunAsync<Fmis.Infra.Auth.AuthStack>();
```

- [ ] **Step 2: Add the dev stack config**

`infra/auth/Pulumi.dev.yaml`:

```yaml
config:
  fmis-auth:enableE2eUser: "true"
```

> `auth0:domain`, `auth0:clientId`, and the secret `auth0:clientSecret` are **not** committed — CI injects them from GitHub environment secrets via `pulumi config set` (Task 9). Locally, a developer sets them with `pulumi config set auth0:domain …` / `pulumi config set --secret auth0:clientSecret …` after `pulumi stack init dev`.

- [ ] **Step 3: Verify the program builds**

Run: `zsh -lc 'cd infra && dotnet build Fmis.Infra.slnx'`
Expected: build succeeds (the program compiles; it is not run here — that needs Auth0 creds + state backend, exercised by CI).

- [ ] **Step 4: Commit**

```bash
git add infra && git commit -m "Add auth stack program entry point and dev config"
```

---

## Task 8: State-backend bootstrap script (idempotent `az` CLI)

**Files:**
- Create: `infra/scripts/bootstrap-state.sh`

- [ ] **Step 1: Write the script**

`infra/scripts/bootstrap-state.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

# Idempotently provision the Pulumi self-managed state backend (azblob).
# Requires an authenticated `az` session (CI uses GitHub->Azure OIDC).
# Env (with defaults):
ENVIRONMENT="${ENVIRONMENT:-dev}"
LOCATION="${AZURE_LOCATION:-eastus}"
RESOURCE_GROUP="${RESOURCE_GROUP:-fmis-${ENVIRONMENT}-infra}"
STORAGE_ACCOUNT="${PULUMI_STATE_ACCOUNT:-fmis${ENVIRONMENT}tfstate}"
CONTAINER="${PULUMI_STATE_CONTAINER:-pulumi-state}"

echo "Ensuring resource group ${RESOURCE_GROUP}..."
az group create --name "${RESOURCE_GROUP}" --location "${LOCATION}" --output none

echo "Ensuring storage account ${STORAGE_ACCOUNT}..."
if ! az storage account show --name "${STORAGE_ACCOUNT}" --resource-group "${RESOURCE_GROUP}" --output none 2>/dev/null; then
  az storage account create \
    --name "${STORAGE_ACCOUNT}" \
    --resource-group "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --min-tls-version TLS1_2 \
    --allow-blob-public-access false \
    --output none
fi

echo "Ensuring container ${CONTAINER}..."
az storage container create \
  --name "${CONTAINER}" \
  --account-name "${STORAGE_ACCOUNT}" \
  --auth-mode login \
  --output none

echo "State backend ready: azblob://${CONTAINER}?storage_account=${STORAGE_ACCOUNT}"
```

> Storage-account names must be globally unique, lowercase, ≤24 chars. `fmisdevtfstate` is 14 chars (fits). If taken globally, set `PULUMI_STATE_ACCOUNT` to a unique value (e.g. add a short suffix) — the script honors the env override. This account is **not** Pulumi-managed (it holds Pulumi's state). It is application/foundation tier, no deletion lock.

- [ ] **Step 2: Make it executable + shellcheck**

```bash
chmod +x infra/scripts/bootstrap-state.sh
zsh -lc 'shellcheck infra/scripts/bootstrap-state.sh' || true
```

(`shellcheck` is advisory; fix any warnings it reports.)

- [ ] **Step 3: Commit**

```bash
git add infra/scripts/bootstrap-state.sh && git commit -m "Add idempotent az CLI Pulumi state-backend bootstrap"
```

---

## Task 9: GitHub Actions CI/CD for the auth stack

**Files:**
- Create: `.github/workflows/infra.yml`

- [ ] **Step 1: Write the workflow**

`.github/workflows/infra.yml`:

```yaml
name: infra

on:
  pull_request:
    branches: [main]
    paths: ["infra/**", ".github/workflows/infra.yml"]
  push:
    branches: [main]
    paths: ["infra/**", ".github/workflows/infra.yml"]

permissions:
  id-token: write   # GitHub -> Azure OIDC
  contents: read

env:
  ENVIRONMENT: dev
  PULUMI_STATE_ACCOUNT: fmisdevtfstate
  PULUMI_STATE_CONTAINER: pulumi-state
  RESOURCE_GROUP: fmis-dev-infra

jobs:
  infra:
    runs-on: ubuntu-latest
    environment: dev
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: infra/global.json

      - name: Build and test infra
        run: dotnet test infra/Fmis.Infra.slnx --configuration Release

      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Bootstrap Pulumi state backend
        run: ./infra/scripts/bootstrap-state.sh

      - uses: pulumi/actions@v6

      - name: Pulumi login (azblob)
        run: pulumi login "azblob://${PULUMI_STATE_CONTAINER}?storage_account=${PULUMI_STATE_ACCOUNT}"

      - name: Select or init the dev stack
        working-directory: infra/auth
        run: pulumi stack select dev || pulumi stack init dev
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}

      - name: Set Auth0 provider config
        working-directory: infra/auth
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
        run: |
          pulumi config set auth0:domain "${{ secrets.AUTH0_DOMAIN }}"
          pulumi config set auth0:clientId "${{ secrets.AUTH0_CLIENT_ID }}"
          pulumi config set --secret auth0:clientSecret "${{ secrets.AUTH0_CLIENT_SECRET }}"

      - name: Preview (pull request)
        if: github.event_name == 'pull_request'
        working-directory: infra/auth
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
        run: pulumi preview

      - name: Up (merge to main)
        if: github.event_name == 'push'
        working-directory: infra/auth
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
        run: pulumi up --yes
```

> Required GitHub config (documented in Task 10): a `dev` **Environment** holding secrets `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` (the OIDC federated identity), `PULUMI_CONFIG_PASSPHRASE`, and `AUTH0_DOMAIN`/`AUTH0_CLIENT_ID`/`AUTH0_CLIENT_SECRET` (the management M2M creds). `azure/login@v2` with `id-token: write` performs OIDC — no stored client secret.

- [ ] **Step 2: Validate the workflow YAML**

```bash
zsh -lc 'python3 -c "import yaml,sys; yaml.safe_load(open(\".github/workflows/infra.yml\"))" && echo OK'
```
Expected: `OK`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/infra.yml && git commit -m "Add GitHub Actions infra pipeline: build/test + bootstrap + preview/up (auth.dev)"
```

---

## Task 10: Tenant-setup runbook

**Files:**
- Create: `docs/auth0-tenant-setup.md`

- [ ] **Step 1: Write the runbook**

`docs/auth0-tenant-setup.md` — document the one-time manual bootstrap, covering both the initial `dev` tenant and adding a future tenant. It MUST contain these sections with concrete steps:

1. **Title + when to use** — "Use this guide to set up a new Auth0 tenant + the manual prerequisites Pulumi can't create."
2. **Create the Auth0 tenant** — sign in to Auth0, create a tenant (name e.g. `modern-fmis-dev`), note the tenant **domain**. Do not delete the default `Username-Password-Authentication` connection (Pulumi sets it as `default_directory`).
3. **Create the management M2M app** — Applications → create a Machine-to-Machine app authorized for the **Auth0 Management API**, granted at least: `read:clients`, `create:clients`, `update:clients`, `delete:clients`, `read:resource_servers`, `create:resource_servers`, `update:resource_servers`, `delete:resource_servers`, `read:users`, `create:users`, `update:users`, `delete:users`, `read:connections`, `update:connections`, `read:tenant_settings`, `update:tenant_settings`. Record its **Client ID** and **Client Secret**.
4. **Create the GitHub→Azure OIDC identity** — `az` CLI commands to: create an Azure AD app registration + service principal, add a **federated credential** for this repository (subject `repo:<org>/modern-fmis:environment:dev` and/or `:ref:refs/heads/main` and `:pull_request`), and assign it **Contributor** on the target subscription. Include the exact `az ad app create`, `az ad app federated-credential create`, and `az role assignment create` commands with placeholders for org/subscription.
5. **Configure GitHub** — create a repository **Environment** named `dev`; add secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (from step 4), `PULUMI_CONFIG_PASSPHRASE` (generate a strong passphrase), `AUTH0_DOMAIN`/`AUTH0_CLIENT_ID`/`AUTH0_CLIENT_SECRET` (from steps 2–3).
6. **Adding another environment later** — repeat steps 2–5 for the new tenant; create a matching Pulumi stack (`pulumi stack init <env>`) and `Pulumi.<env>.yaml`; extend the workflow `env`/matrix. Naming stays `fmis-<env>-<layer>-<resource>` and the audience `https://<env>.api.modern-fmis`.
7. **Local development** — `pulumi login azblob://…`, `pulumi stack select dev`, set `auth0:*` config locally, `pulumi preview`. Requires the passphrase + Azure login.

Write full, copy-pasteable `az` commands in sections 4 (with `<org>`, `<subscription-id>` placeholders the operator fills in). No "TBD".

- [ ] **Step 2: Commit**

```bash
git add docs/auth0-tenant-setup.md && git commit -m "Add Auth0 tenant-setup runbook (manual bootstrap: tenant, M2M, OIDC, secrets)"
```

---

## Done criteria

- `cd infra && zsh -lc 'dotnet test Fmis.Infra.slnx'` is green: `ResourceNamesTests` + `AuthStackTests` (SPA, API+audience, default_directory, conditional e2e on/off, outputs).
- `dotnet build Fmis.Infra.slnx` succeeds (the `auth` program compiles).
- `bootstrap-state.sh` is idempotent and shellcheck-clean; `.github/workflows/infra.yml` is valid YAML.
- `docs/auth0-tenant-setup.md` is followable end-to-end (tenant, M2M, OIDC identity, GitHub secrets) with concrete `az` commands.
- Naming everywhere via `ResourceNames` (`fmis-dev-auth-*`, audience `https://dev.api.modern-fmis`); single `dev` environment, structured so another is additive.
- **Deferred (3b/3c):** persistence/application stacks, `config.json` emission, deployed-URL callbacks, the Playwright suite + live login.
