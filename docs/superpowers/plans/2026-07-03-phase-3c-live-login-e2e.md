# Phase 3c — Live Integrated Login + Playwright E2E — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the SPA Auth0 client into the application stack, add a top-level `e2e/` pnpm workspace with a Playwright suite (one real interactive login + token-short-circuit tests), relocate the contract test into it, and restructure CD into gate → deploy → verify so dev is auto-verified end-to-end on every deploy.

**Architecture:** The auth stack keeps the shared platform (tenant, API/resource server, e2e access) and gains a dev attack-protection relaxation; the application stack creates and configures its own SPA client (callbacks = localhost + deployed frontend URL). A new `e2e/` pnpm workspace holds Playwright system tests, shared utilities, and the relocated contract test (rewritten onto Playwright's runner). CD gets a single `environment`-gated approval job whose outputs drive un-gated deploy + verify jobs.

**Tech Stack:** Pulumi C# (Pulumi 3.107.3, Pulumi.AzureNative 3.19.0, Pulumi.Auth0 3.45.0), .NET 10, Pulumi.Testing/xUnit, pnpm workspaces (Corepack, pnpm 11.7.0, Node 24), Playwright, `@auth0/auth0-react` 2.18.0 / `@auth0/auth0-spa-js` 2.21.1, GitHub Actions.

## Global Constraints

- No code comments — self-documenting names; rationale lives in docs.
- Exact pinned versions — `Pulumi.Auth0` **3.45.0**; `@playwright/test` pinned to **1.56.0**; existing pins unchanged.
- TDD for all infra (Pulumi.Testing) and the contract suite; run `dotnet` from repo root (`Fmis.slnx`); shell via `zsh -lc`.
- Append-only git — new commits only, never amend/force-push. Commit trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- ComponentResource composition + thin stacks; naming `ResourceNames.For(env, layer, resource)`.
- Branch: `phase-3c-live-login-e2e` (already created; spec already committed on it).
- pnpm via Corepack, Node 24; run pnpm from the **repo root** after the workspace is introduced (Task 5).
- The e2e **system** tests and the CI/CD YAML cannot be executed against deployed dev during implementation. Verify them by `pnpm --filter e2e exec playwright test --list`, `pnpm --filter e2e run typecheck`, `pnpm --filter e2e run lint`, and YAML parse. Runtime verification happens on the first CD run after merge (call this out in the final task). The **contract** suite and all **infra** tasks ARE fully runnable during implementation.

**Operator prerequisite (not code, do not block on it):** Before the first CD run, an operator must add an Azure federated credential for subject `repo:uplift-klinker/modern-fmis:ref:refs/heads/main` (documented in Task 12).

---

## File Structure

**Part 1 — auth (`infra/auth`)**
- Modify `Components/TenantConfiguration.cs` — add `Auth0.AttackProtection` (dev relaxation).
- Delete `Components/SpaApplication.cs`.
- Modify `AuthStack.cs` — drop `SpaApplication` + `spaClientId` output.
- Modify `../tests/Fmis.Infra.Tests/AuthStackTests.cs`.

**Part 2 — application (`infra/application`)**
- Modify `Fmis.Infra.Application.csproj` — add `Pulumi.Auth0` 3.45.0.
- Create `Components/SpaClient.cs`.
- Modify `ApplicationStack.cs` — wire `SpaClient`, feed its id to `FrontendSite.WriteConfig`.
- Modify `../tests/Fmis.Infra.Tests/StackMocks.cs` — auth0 client-id + storage `primaryEndpoints`; drop `spaClientId` from the auth ref.
- Modify `../tests/Fmis.Infra.Tests/ApplicationStackTests.cs`.

**Part 3 — workspace + e2e (`/`, `e2e/`, `frontend/`)**
- Create `pnpm-workspace.yaml`, root `package.json`.
- Modify `frontend/package.json` (drop `packageManager`, drop `test:contract`), delete `frontend/vitest.contract.config.ts`, `frontend/contract.setup.ts`, `frontend/src/features/clients/schemas/client-contract.contract.ts`.
- Create `e2e/package.json`, `e2e/tsconfig.json`, `e2e/playwright.config.ts`, `e2e/playwright.contract.config.ts`, `e2e/eslint.config.js`.
- Create `e2e/support/config.ts`, `e2e/support/auth.ts`, `e2e/support/api.ts`.
- Create `e2e/system/login.smoke.spec.ts`, `e2e/system/authenticated-app.spec.ts`, `e2e/system/api.spec.ts`.
- Create `e2e/contract/global-setup.ts`, `e2e/contract/global-teardown.ts`, `e2e/contract/client-contract.spec.ts`.
- Modify `Fmis.slnx` — add an `e2e` solution folder.

**Part 4 — CI/CD (`.github/workflows`)**
- Modify `ci.yml` (workspace-filtered frontend + contract jobs).
- Modify `cd.yml` (gate → deploy → verify).
- Modify `pulumi-stacks.yml` (conditional `environment`).
- Modify `docs/conventions/` runbook (operator federated-credential note); update roadmap line in `docs/conventions/architecture.md`.

---

## Part 1 — Auth re-slice

### Task 1: Remove the SPA client from the auth stack

**Files:**
- Modify: `infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`
- Modify: `infra/auth/AuthStack.cs`
- Delete: `infra/auth/Components/SpaApplication.cs`

**Interfaces:**
- Produces: `AuthStack` no longer exposes `[Output("spaClientId")]`. Auth still exposes `domain`, `audience`, `e2eClientId/Secret/Username/Password`.
- Consumes: nothing new.

- [ ] **Step 1: Update the auth tests to expect no SPA client**

In `infra/auth/tests` file `AuthStackTests.cs`: delete the test `Creates_the_spa_application_named_for_the_environment` (lines 7-15) and the test `Declares_the_spa_as_a_public_client_with_no_token_endpoint_auth` (lines 94-101). Replace the body of `Exposes_domain_spa_client_id_and_audience_outputs` and rename it, and add a new test asserting the SPA is gone. Concretely, replace:

```csharp
    [Fact]
    public async Task Exposes_domain_spa_client_id_and_audience_outputs()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);
        var stack = resources.OfType<Fmis.Infra.Auth.AuthStack>().Single();

        Assert.Equal("dev.modern-fmis.auth0.com", await InfraTesting.GetAsync(stack.Domain));
        Assert.Equal("https://dev.api.modern-fmis", await InfraTesting.GetAsync(stack.Audience));
        Assert.NotNull(await InfraTesting.GetAsync(stack.SpaClientId));
    }
```

with:

```csharp
    [Fact]
    public async Task Exposes_domain_and_audience_outputs()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);
        var stack = resources.OfType<Fmis.Infra.Auth.AuthStack>().Single();

        Assert.Equal("dev.modern-fmis.auth0.com", await InfraTesting.GetAsync(stack.Domain));
        Assert.Equal("https://dev.api.modern-fmis", await InfraTesting.GetAsync(stack.Audience));
    }

    [Fact]
    public async Task Does_not_create_a_spa_application()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        Assert.DoesNotContain(
            resources.OfType<Auth0.Client>(),
            c => InfraTesting.GetAsync(c.AppType).Result == "spa");
    }
```

- [ ] **Step 2: Run the auth tests to verify they fail to compile / fail**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Fmis.Infra.Tests.AuthStackTests'"`
Expected: FAIL — compile error on `stack.SpaClientId` (still referenced?) is already removed from the test, but `AuthStack` still defines `SpaClientId` + creates the SPA, so `Does_not_create_a_spa_application` FAILS (a spa client is present).

- [ ] **Step 3: Remove the SPA client from `AuthStack.cs`**

In `infra/auth/AuthStack.cs`: delete the `[Output("spaClientId")]` property (line 10), delete the `var spa = new SpaApplication(...)` line (21), delete the `SpaClientId = spa.ClientId;` line (28). Result:

```csharp
using Pulumi;
using Fmis.Infra.Common;
using Fmis.Infra.Auth.Components;

namespace Fmis.Infra.Auth;

public class AuthStack : Stack
{
    [Output("domain")] public Output<string> Domain { get; private set; }
    [Output("audience")] public Output<string> Audience { get; private set; }
    [Output("e2eClientId")] public Output<string?> E2eClientId { get; private set; }
    [Output("e2eClientSecret")] public Output<string?> E2eClientSecret { get; private set; }
    [Output("e2eUsername")] public Output<string?> E2eUsername { get; private set; }
    [Output("e2ePassword")] public Output<string?> E2ePassword { get; private set; }

    public AuthStack()
    {
        var env = Deployment.Instance.StackName;

        var api = new AuthApi(ResourceNames.For(env, "auth", "api"), ResourceNames.Audience(env));
        _ = new TenantConfiguration(ResourceNames.For(env, "auth", "tenant"));

        Domain = Output.Create(
            Environment.GetEnvironmentVariable("AUTH0_DOMAIN")
            ?? throw new InvalidOperationException("AUTH0_DOMAIN environment variable is required."));
        Audience = api.Audience;

        E2eClientId = Output.CreateSecret((string?)null);
        E2eClientSecret = Output.CreateSecret((string?)null);
        E2eUsername = Output.CreateSecret((string?)null);
        E2ePassword = Output.CreateSecret((string?)null);

        if (new Config().GetBoolean("enableE2eUser") ?? false)
        {
            var e2e = new E2eTestAccess(ResourceNames.For(env, "auth", "e2e"));
            E2eClientId = Output.CreateSecret(e2e.ClientId.Apply(value => (string?)value));
            E2eClientSecret = Output.CreateSecret(e2e.ClientSecret.Apply(value => (string?)value));
            E2eUsername = Output.CreateSecret(e2e.Username.Apply(value => (string?)value));
            E2ePassword = Output.CreateSecret(e2e.Password.Apply(value => (string?)value));
        }
    }
}
```

- [ ] **Step 4: Delete the SpaApplication component**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git rm infra/auth/Components/SpaApplication.cs"`

- [ ] **Step 5: Run the auth tests to verify they pass**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Fmis.Infra.Tests.AuthStackTests'"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A infra/auth infra/tests && git commit -m 'Remove the SPA client from the auth stack

The SPA client is specific to the deployed frontend and needs the deployed
frontend URL as a callback, so it moves to the application stack (next task).
Auth keeps the tenant, the API resource server, and e2e access.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 2: Relax dev attack protection in the auth tenant

**Files:**
- Modify: `infra/tests/Fmis.Infra.Tests/AuthStackTests.cs`
- Modify: `infra/auth/Components/TenantConfiguration.cs`

**Interfaces:**
- Produces: the auth stack now contains one `Auth0.AttackProtection` resource with brute-force + suspicious-IP throttling disabled.

- [ ] **Step 1: Write the failing test**

Add to `AuthStackTests.cs`:

```csharp
    [Fact]
    public async Task Relaxes_attack_protection_for_automated_logins()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        var protection = resources.OfType<Auth0.AttackProtection>().Single();
        Assert.False(await InfraTesting.GetAsync(protection.BruteForceProtection.Apply(b => b!.Enabled!.Value)));
        Assert.False(await InfraTesting.GetAsync(protection.SuspiciousIpThrottling.Apply(s => s!.Enabled!.Value)));
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Relaxes_attack_protection_for_automated_logins'"`
Expected: FAIL — `Single()` throws (no `AttackProtection` resource exists).

- [ ] **Step 3: Add the AttackProtection resource**

Replace `infra/auth/Components/TenantConfiguration.cs` with:

```csharp
using Pulumi;
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Auth.Components;

public sealed class TenantConfiguration : ComponentResource
{
    public TenantConfiguration(string name, ComponentResourceOptions? options = null)
        : base("fmis:auth:TenantConfiguration", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        _ = new Auth0.Tenant(name, new Auth0.TenantArgs
        {
            DefaultDirectory = "Username-Password-Authentication",
        }, childOptions);

        _ = new Auth0.AttackProtection($"{name}-attack-protection", new Auth0.AttackProtectionArgs
        {
            BruteForceProtection = new Auth0.Inputs.AttackProtectionBruteForceProtectionArgs
            {
                Enabled = false,
            },
            SuspiciousIpThrottling = new Auth0.Inputs.AttackProtectionSuspiciousIpThrottlingArgs
            {
                Enabled = false,
            },
        }, childOptions);

        RegisterOutputs();
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Fmis.Infra.Tests.AuthStackTests'"`
Expected: PASS (all auth tests).

- [ ] **Step 5: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A infra/auth infra/tests && git commit -m 'Relax dev tenant attack protection for automated logins

Suspicious-IP throttling and brute-force protection can block repeated
automated interactive logins from CI runners; disable them so the Playwright
smoke login is not intermittently throttled. Scoped to the dev tenant.
Bot-detection/CAPTCHA is verified on the first live run and adjusted here if
it surfaces.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

---

## Part 2 — Application stack owns the SPA client

### Task 3: Create and wire the SPA client in the application stack

**Files:**
- Modify: `infra/application/Fmis.Infra.Application.csproj`
- Create: `infra/application/Components/SpaClient.cs`
- Modify: `infra/application/ApplicationStack.cs`
- Modify: `infra/tests/Fmis.Infra.Tests/StackMocks.cs`
- Modify: `infra/tests/Fmis.Infra.Tests/ApplicationStackTests.cs`

**Interfaces:**
- Consumes: `frontendSite.Url` (`Output<string>` = the deployed static-site web endpoint), auth-ref `domain`/`audience`.
- Produces: `SpaClient` with `public Output<string> ClientId { get; }`; `ApplicationStack` writes `config.json` with this local client id (no longer reads `spaClientId` from auth).

- [ ] **Step 1: Add the Pulumi.Auth0 package reference**

In `infra/application/Fmis.Infra.Application.csproj`, add inside the first `<ItemGroup>` (after the `Pulumi.SyncedFolder` line):

```xml
    <PackageReference Include="Pulumi.Auth0" Version="3.45.0" />
```

- [ ] **Step 2: Extend `StackMocks` so Auth0 clients and the storage endpoint resolve**

In `infra/tests/Fmis.Infra.Tests/StackMocks.cs`:

(a) In the `else` branch of the StackReference block (the persistence outputs, lines 24-30) leave as-is. In the `auth` branch (lines 13-14) it currently returns `spaClientId`; drop it so the app can't accidentally depend on it. Replace that line with:

```csharp
            if (args.Name.Contains("auth"))
                outputs = new() { ["domain"] = "fmis-dev.us.auth0.com", ["audience"] = "https://dev.api.modern-fmis" };
```

(b) In the generic resource branch (after `AddProviderState(state, args);`, before the `return`), add Auth0 client-id + storage endpoint mocks. Replace the block:

```csharp
        var state = args.Inputs.ToBuilder();
        foreach (var key in new[] { "resourceName", "configurationName", "databaseName", "serverName", "lockName", "registryName" })
        {
            if (state.TryGetValue(key, out var value))
            {
                state["name"] = value;
                if (key == "serverName")
                    state["fullyQualifiedDomainName"] = $"{value}.postgres.database.azure.com";
                break;
            }
        }
        AddProviderState(state, args);
        return Task.FromResult<(string?, object)>(($"{args.Name}_id", state.ToImmutable()));
```

with:

```csharp
        var state = args.Inputs.ToBuilder();
        foreach (var key in new[] { "resourceName", "configurationName", "databaseName", "serverName", "lockName", "registryName" })
        {
            if (state.TryGetValue(key, out var value))
            {
                state["name"] = value;
                if (key == "serverName")
                    state["fullyQualifiedDomainName"] = $"{value}.postgres.database.azure.com";
                break;
            }
        }
        if (args.Type == "auth0:index/client:Client")
            state["clientId"] = $"{args.Name}_client_id";
        if (args.Type == "azure-native:storage:StorageAccount")
            state["primaryEndpoints"] = new Dictionary<string, object> { ["web"] = "https://fmisdevweb.z00.web.core.windows.net/" };
        AddProviderState(state, args);
        return Task.FromResult<(string?, object)>(($"{args.Name}_id", state.ToImmutable()));
```

- [ ] **Step 3: Write the failing application-stack tests**

In `ApplicationStackTests.cs`, add these tests (the `using AzureNative = Pulumi.AzureNative;` and `using Fmis.Infra.Application.Components;` imports already exist; add `using Auth0 = Pulumi.Auth0;` at the top):

```csharp
    [Fact]
    public async Task Creates_a_spa_client_allowing_localhost_and_the_deployed_site()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var spa = resources.OfType<Auth0.Client>().Single(c => InfraTesting.GetAsync(c.AppType).Result == "spa");
        var callbacks = await InfraTesting.GetAsync(spa.Callbacks);
        Assert.Contains("http://localhost:5173", callbacks);
        Assert.Contains("https://fmisdevweb.z00.web.core.windows.net/", callbacks);
    }

    [Fact]
    public async Task Declares_the_spa_as_a_public_client_with_no_token_endpoint_auth()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var credentials = resources.OfType<Auth0.ClientCredentials>().Single();
        Assert.Equal("none", await InfraTesting.GetAsync(credentials.AuthenticationMethod));
    }
```

Also update `Writes_a_config_json_blob_with_the_spa_settings` to assert the config carries a client id (it already asserts `apiBaseUrl`/`audience`; add a `clientId` check). Change its body to also assert:

```csharp
        Assert.Contains("\"clientId\"", json);
```

- [ ] **Step 4: Run to verify they fail**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Fmis.Infra.Tests.ApplicationStackTests'"`
Expected: FAIL — no `Auth0.Client`/`Auth0.ClientCredentials` exist yet; and the current stack reads `auth.RequireString("spaClientId")` which is no longer mocked (the auth ref dropped it), so the config test may also fail.

- [ ] **Step 5: Create the `SpaClient` component**

Create `infra/application/Components/SpaClient.cs`:

```csharp
using Pulumi;
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Application.Components;

public sealed class SpaClient : ComponentResource
{
    public Output<string> ClientId { get; }

    public SpaClient(string name, Input<string> frontendUrl, ComponentResourceOptions? options = null)
        : base("fmis:application:SpaClient", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var callbacks = frontendUrl.ToOutput().Apply(url => new[] { "http://localhost:5173", url });

        var client = new Auth0.Client(name, new Auth0.ClientArgs
        {
            Name = name,
            AppType = "spa",
            OidcConformant = true,
            GrantTypes = { "authorization_code", "refresh_token" },
            Callbacks = callbacks,
            AllowedLogoutUrls = callbacks,
            WebOrigins = callbacks,
        }, childOptions);

        _ = new Auth0.ClientCredentials($"{name}-creds", new Auth0.ClientCredentialsArgs
        {
            ClientId = client.ClientId,
            AuthenticationMethod = "none",
        }, childOptions);

        ClientId = client.ClientId;
        RegisterOutputs();
    }
}
```

- [ ] **Step 6: Wire `SpaClient` into `ApplicationStack` and feed its id to the config**

In `infra/application/ApplicationStack.cs`, after the `frontendSite` is created (currently line 49) and before `WriteConfig` (line 68), create the SPA client; then change `WriteConfig`'s `spaClientId` argument from `auth.RequireString("spaClientId")` to the local client id. Concretely:

Replace:
```csharp
        var frontendSite = new FrontendSite($"fmis{env}web", resourceGroup.Name, location);
```
with:
```csharp
        var frontendSite = new FrontendSite($"fmis{env}web", resourceGroup.Name, location);

        var spaClient = new SpaClient($"fmis-{env}-spa", frontendSite.Url);
```

Replace:
```csharp
        frontendSite.WriteConfig(
            backendUrl: backend.Url,
            authDomain: auth.RequireString("domain"),
            spaClientId: auth.RequireString("spaClientId"),
            audience: auth.RequireString("audience"));
```
with:
```csharp
        frontendSite.WriteConfig(
            backendUrl: backend.Url,
            authDomain: auth.RequireString("domain"),
            spaClientId: spaClient.ClientId,
            audience: auth.RequireString("audience"));
```

- [ ] **Step 7: Run the application tests to verify they pass**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Fmis.Infra.Tests.ApplicationStackTests'"`
Expected: PASS.

- [ ] **Step 8: Run the whole infra suite to confirm nothing regressed**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Fmis.Infra'"`
Expected: PASS (all infra tests).

- [ ] **Step 9: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A infra/application infra/tests && git commit -m 'Own the SPA Auth0 client in the application stack

The application stack now creates its own spa client (callbacks/logout/web
origins = localhost + the deployed frontend URL) and feeds the local client id
into config.json, replacing the auth-stack spaClientId output.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

---

## Part 3 — Workspace + e2e

### Task 4: Introduce the pnpm workspace root

**Files:**
- Create: `pnpm-workspace.yaml`
- Create: `package.json` (repo root)
- Modify: `frontend/package.json` (remove `packageManager`)

**Interfaces:**
- Produces: a root pnpm workspace with member `frontend` (and `e2e`, added next task). `pnpm install` runs from the repo root.

- [ ] **Step 1: Create `pnpm-workspace.yaml`**

```yaml
packages:
  - frontend
  - e2e
```

- [ ] **Step 2: Create the root `package.json`**

```json
{
  "name": "modern-fmis",
  "private": true,
  "packageManager": "pnpm@11.7.0",
  "engines": {
    "node": ">=24 <25"
  }
}
```

- [ ] **Step 3: Remove the `packageManager` pin from the frontend package**

In `frontend/package.json`, delete the line `"packageManager": "pnpm@11.7.0",` (line 49). Leave `engines` in place.

- [ ] **Step 4: Verify the workspace installs**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && corepack enable && pnpm install"`
Expected: PASS — resolves the `frontend` workspace (the `e2e` package doesn't exist yet; pnpm warns but succeeds, or ignores the missing dir). If pnpm errors on the missing `e2e` dir, that is resolved in Task 5; for this task confirm `frontend` resolves by running `pnpm --filter frontend run typecheck`.

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter frontend run typecheck"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A pnpm-workspace.yaml package.json frontend/package.json pnpm-lock.yaml && git commit -m 'Make the repo a pnpm workspace (frontend + e2e)

Move the Corepack packageManager pin to a root package.json and add
pnpm-workspace.yaml so frontend and the new e2e workspace install together
without cross-directory pathing.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 5: Scaffold the e2e workspace (package, tsconfig, Playwright configs, lint)

**Files:**
- Create: `e2e/package.json`, `e2e/tsconfig.json`, `e2e/playwright.config.ts`, `e2e/playwright.contract.config.ts`, `e2e/eslint.config.js`

**Interfaces:**
- Produces: `pnpm --filter e2e` with scripts `test:e2e`, `test:smoke`, `test:contract`, `typecheck`, `lint`. `e2e` depends on `frontend` (`workspace:*`). Path alias `@/*` → `../frontend/src/*` resolves in Playwright + tsc.

- [ ] **Step 1: Create `e2e/package.json`**

```json
{
  "name": "e2e",
  "private": true,
  "type": "module",
  "scripts": {
    "test:e2e": "playwright test --config playwright.config.ts",
    "test:smoke": "playwright test --config playwright.config.ts --grep @smoke",
    "test:contract": "playwright test --config playwright.contract.config.ts",
    "typecheck": "tsc --noEmit",
    "lint": "eslint ."
  },
  "dependencies": {
    "frontend": "workspace:*"
  },
  "devDependencies": {
    "@playwright/test": "1.56.0",
    "@types/node": "24.13.2",
    "typescript": "6.0.3",
    "eslint": "10.5.0",
    "typescript-eslint": "8.61.0",
    "@eslint/js": "10.0.1",
    "globals": "17.6.0",
    "zod": "4.4.3"
  }
}
```

- [ ] **Step 2: Create `e2e/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "types": ["node"],
    "baseUrl": ".",
    "paths": {
      "@/*": ["../frontend/src/*"]
    }
  },
  "include": ["support", "system", "contract", "playwright.config.ts", "playwright.contract.config.ts"]
}
```

- [ ] **Step 3: Create `e2e/playwright.config.ts` (system suite, deployed dev)**

```typescript
import { defineConfig, devices } from '@playwright/test';
import { requireEnv } from './support/config';

export default defineConfig({
  testDir: './system',
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : [['list']],
  use: {
    baseURL: requireEnv('E2E_FRONTEND_URL'),
    trace: 'on-first-retry',
  },
  projects: [{ name: 'system', use: { ...devices['Desktop Chrome'] } }],
});
```

- [ ] **Step 4: Create `e2e/playwright.contract.config.ts` (contract suite, local backend)**

```typescript
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './contract',
  globalSetup: './contract/global-setup.ts',
  globalTeardown: './contract/global-teardown.ts',
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : [['list']],
  use: {
    baseURL: 'http://localhost:8080',
  },
  timeout: 30_000,
  projects: [{ name: 'contract' }],
});
```

- [ ] **Step 5: Create `e2e/eslint.config.js`**

```javascript
import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import globals from 'globals';

export default tseslint.config(
  { ignores: ['node_modules', 'playwright-report', 'test-results'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    languageOptions: {
      globals: { ...globals.node },
    },
  },
);
```

- [ ] **Step 6: Install and verify the workspace resolves (no tests yet)**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm install && pnpm --filter e2e exec playwright install chromium"`
Expected: PASS — `e2e` resolves and links `frontend`; Chromium downloads.

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter e2e run typecheck"`
Expected: PASS (no source files yet beyond configs; tsc succeeds).

- [ ] **Step 7: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A e2e pnpm-lock.yaml && git commit -m 'Scaffold the e2e workspace (Playwright + configs + lint)

System suite (deployed dev) and contract suite (local backend) configs, a
tsconfig mapping @/* to the frontend source, and a frontend workspace
dependency for schema reuse.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 6: e2e support utilities

**Files:**
- Create: `e2e/support/config.ts`, `e2e/support/auth.ts`, `e2e/support/api.ts`

**Interfaces:**
- Produces:
  - `config.ts`: `requireEnv(name: string): string`; `e2eConfig(): { frontendUrl, backendUrl, authDomain, audience, clientId, clientSecret, username, password }` (all strings, from env).
  - `auth.ts`: `generateToken(): Promise<{ accessToken: string; idToken: string; expiresIn: number; scope: string }>`; `interactiveLogin(page: Page): Promise<void>`; `seedAuthSession(page: Page, token): Promise<void>`.
  - `api.ts`: `authorizedRequest(accessToken: string): Promise<APIRequestContext>`.
- Consumes: `E2E_*` env vars (Task 8 CD job supplies them).

- [ ] **Step 1: Create `e2e/support/config.ts`**

```typescript
export function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Required environment variable ${name} is not set.`);
  }
  return value;
}

export interface E2eConfig {
  frontendUrl: string;
  backendUrl: string;
  authDomain: string;
  audience: string;
  clientId: string;
  e2eClientId: string;
  clientSecret: string;
  username: string;
  password: string;
}

export function e2eConfig(): E2eConfig {
  return {
    frontendUrl: requireEnv('E2E_FRONTEND_URL'),
    backendUrl: requireEnv('E2E_BACKEND_URL'),
    authDomain: requireEnv('E2E_AUTH_DOMAIN'),
    audience: requireEnv('E2E_AUTH_AUDIENCE'),
    clientId: requireEnv('E2E_SPA_CLIENT_ID'),
    e2eClientId: requireEnv('E2E_CLIENT_ID'),
    clientSecret: requireEnv('E2E_CLIENT_SECRET'),
    username: requireEnv('E2E_USERNAME'),
    password: requireEnv('E2E_PASSWORD'),
  };
}
```

`clientId` (`E2E_SPA_CLIENT_ID`) is the SPA client the app uses — `seedAuthSession` writes the auth0-spa-js cache under it. `e2eClientId` (`E2E_CLIENT_ID`) is the non-interactive password-grant client — `generateToken` exchanges credentials through it.

- [ ] **Step 2: Create `e2e/support/auth.ts`**

```typescript
import type { Page } from '@playwright/test';
import { e2eConfig } from './config';

const SCOPE = 'openid profile email';

export interface E2eToken {
  accessToken: string;
  idToken: string;
  expiresIn: number;
  scope: string;
}

export async function generateToken(): Promise<E2eToken> {
  const config = e2eConfig();
  const response = await fetch(`https://${config.authDomain}/oauth/token`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      grant_type: 'http://auth0.com/oauth/grant-type/password-realm',
      realm: 'Username-Password-Authentication',
      client_id: config.e2eClientId,
      client_secret: config.clientSecret,
      username: config.username,
      password: config.password,
      audience: config.audience,
      scope: SCOPE,
    }),
  });
  if (!response.ok) {
    throw new Error(`Auth0 token request failed: ${response.status} ${await response.text()}`);
  }
  const body = (await response.json()) as {
    access_token: string;
    id_token: string;
    expires_in: number;
    scope?: string;
  };
  return {
    accessToken: body.access_token,
    idToken: body.id_token,
    expiresIn: body.expires_in,
    scope: body.scope ?? SCOPE,
  };
}

export async function interactiveLogin(page: Page): Promise<void> {
  const config = e2eConfig();
  await page.goto('/');
  await page.getByLabel('Email address').fill(config.username);
  await page.getByLabel('Password').fill(config.password);
  await page.getByRole('button', { name: 'Continue', exact: false }).click();
  await page.waitForURL('**/welcome');
}

function decodeJwtPayload(token: string): Record<string, unknown> {
  const payload = token.split('.')[1];
  const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
  const json = Buffer.from(normalized, 'base64').toString('utf8');
  return JSON.parse(json) as Record<string, unknown>;
}

export async function seedAuthSession(page: Page, token: E2eToken): Promise<void> {
  const config = e2eConfig();
  const claims = decodeJwtPayload(token.idToken);
  const key = ['@@auth0spajs@@', config.clientId, config.audience, token.scope]
    .filter(Boolean)
    .join('::');
  const entry = {
    body: {
      access_token: token.accessToken,
      id_token: token.idToken,
      token_type: 'Bearer',
      expires_in: token.expiresIn,
      audience: config.audience,
      scope: token.scope,
      client_id: config.clientId,
      decodedToken: { claims, user: claims },
    },
    expiresAt: Math.floor(Date.now() / 1000) + token.expiresIn,
  };
  await page.addInitScript(
    ([storageKey, storageValue]) => {
      window.localStorage.setItem(storageKey, storageValue);
    },
    [key, JSON.stringify(entry)] as const,
  );
}
```

Note: `interactiveLogin` selectors target the Auth0 Universal Login form (`Email address` / `Password` labels, `Continue`/`Log In` button). These are Auth0-hosted; the exact button label is verified on the first live run and adjusted here if needed (single point of change). `seedAuthSession` encodes the confirmed `@auth0/auth0-spa-js` 2.21.1 cache format; it is the single documented place to adjust if the cache manifest behavior differs on the first live run.

- [ ] **Step 3: Create `e2e/support/api.ts`**

```typescript
import { request, type APIRequestContext } from '@playwright/test';
import { e2eConfig } from './config';

export async function authorizedRequest(accessToken: string): Promise<APIRequestContext> {
  const config = e2eConfig();
  return request.newContext({
    baseURL: config.backendUrl,
    extraHTTPHeaders: { authorization: `Bearer ${accessToken}` },
  });
}
```

- [ ] **Step 4: Typecheck + lint the support utilities**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter e2e run typecheck && pnpm --filter e2e run lint"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A e2e && git commit -m 'Add e2e support utilities (config, auth, api)

Env-based config discovery, an Auth0 password-realm token generator, an
interactive-login helper, an auth0-spa-js localStorage cache seeder, and an
authorized API request context.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 7: e2e system specs

**Files:**
- Create: `e2e/system/login.smoke.spec.ts`, `e2e/system/authenticated-app.spec.ts`, `e2e/system/api.spec.ts`

**Interfaces:**
- Consumes: `support/auth.ts`, `support/api.ts`.
- Produces: three Playwright tests; exactly one tagged `@smoke`.

- [ ] **Step 1: Create `e2e/system/login.smoke.spec.ts`**

```typescript
import { test, expect } from '@playwright/test';
import { interactiveLogin } from '../support/auth';
import { e2eConfig } from '../support/config';

test('interactive Auth0 login reaches the authenticated app @smoke', async ({ page }) => {
  await interactiveLogin(page);

  await expect(page.getByRole('heading', { name: 'Welcome to modern-fmis' })).toBeVisible();
  await expect(page.getByRole('button', { name: e2eConfig().username })).toBeVisible();
});
```

- [ ] **Step 2: Create `e2e/system/authenticated-app.spec.ts`**

```typescript
import { test, expect } from '@playwright/test';
import { generateToken, seedAuthSession } from '../support/auth';

test('a generated token short-circuits login and renders the authenticated app', async ({ page }) => {
  const token = await generateToken();
  await seedAuthSession(page, token);

  await page.goto('/welcome');

  await expect(page.getByRole('heading', { name: 'Welcome to modern-fmis' })).toBeVisible();
  await expect(page.getByRole('button', { name: /@/ })).toBeVisible();
});
```

- [ ] **Step 3: Create `e2e/system/api.spec.ts`**

```typescript
import { test, expect } from '@playwright/test';
import { generateToken } from '../support/auth';
import { authorizedRequest } from '../support/api';

test('a generated Auth0 token is accepted by the deployed backend', async () => {
  const token = await generateToken();
  const api = await authorizedRequest(token.accessToken);

  const response = await api.get('/clients');

  expect(response.status()).toBe(200);
  await api.dispose();
});
```

- [ ] **Step 4: List the tests to verify they compile and are discovered**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && E2E_FRONTEND_URL=http://placeholder pnpm --filter e2e exec playwright test --config playwright.config.ts --list"`
Expected: PASS — lists 3 tests, exactly one with `@smoke`. (The env var is only needed so the config's `requireEnv('E2E_FRONTEND_URL')` doesn't throw during config load; `--list` does not run them.)

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter e2e run typecheck && pnpm --filter e2e run lint"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A e2e && git commit -m 'Add e2e system specs (interactive smoke + token short-circuit + API)

One real interactive-login smoke test (in both suites via @smoke), a
token-seeded authenticated-app test, and a token->backend API acceptance test.
System suite runs against deployed dev.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 8: Relocate the contract test onto Playwright and remove it from frontend

**Files:**
- Create: `e2e/contract/global-setup.ts`, `e2e/contract/global-teardown.ts`, `e2e/contract/client-contract.spec.ts`
- Delete: `frontend/vitest.contract.config.ts`, `frontend/contract.setup.ts`, `frontend/src/features/clients/schemas/client-contract.contract.ts`
- Modify: `frontend/package.json` (remove `test:contract` script)
- Modify: `Fmis.slnx` (add `e2e` solution folder)

**Interfaces:**
- Consumes: the frontend Zod schemas via the `frontend` workspace dependency + the `@/*` path alias.
- Produces: `pnpm --filter e2e test:contract` spins up the backend via `docker compose`, validates Zod ↔ live `/openapi/v1.json`.

- [ ] **Step 1: Create `e2e/contract/global-setup.ts`**

```typescript
import { execFileSync } from 'node:child_process';
import { fileURLToPath, URL } from 'node:url';

const repoRoot = fileURLToPath(new URL('../../', import.meta.url));
const apiUrl = 'http://localhost:8080';

async function waitForApi(timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${apiUrl}/openapi/v1.json`);
      if (response.ok) {
        return;
      }
    } catch {
      // backend is not accepting connections yet
    }
    await new Promise((resolve) => setTimeout(resolve, 1000));
  }
  throw new Error(`Backend API did not become ready at ${apiUrl} within ${timeoutMs}ms`);
}

export default async function globalSetup(): Promise<void> {
  execFileSync('docker', ['compose', 'up', '-d', '--build', 'backend'], {
    cwd: repoRoot,
    stdio: 'inherit',
  });
  await waitForApi(120_000);
}
```

- [ ] **Step 2: Create `e2e/contract/global-teardown.ts`**

```typescript
import { execFileSync } from 'node:child_process';
import { fileURLToPath, URL } from 'node:url';

const repoRoot = fileURLToPath(new URL('../../', import.meta.url));

export default async function globalTeardown(): Promise<void> {
  execFileSync('docker', ['compose', 'down'], { cwd: repoRoot, stdio: 'inherit' });
}
```

- [ ] **Step 3: Create `e2e/contract/client-contract.spec.ts`**

```typescript
import { test, expect } from '@playwright/test';
import {
  ClientResponseSchema,
  ClientListSchema,
  CreateClientRequestObjectSchema,
} from '@/features/clients/schemas/client-schemas';

interface OpenApiDocument {
  components?: { schemas?: Record<string, { properties?: Record<string, unknown> }> };
}

async function propertyNamesOf(schemaName: string): Promise<string[]> {
  const response = await fetch('http://localhost:8080/openapi/v1.json');
  const openapi = (await response.json()) as OpenApiDocument;
  return Object.keys(openapi.components?.schemas?.[schemaName]?.properties ?? {}).sort();
}

test('ClientResponseSchema matches ClientResponseModel', async () => {
  expect(Object.keys(ClientResponseSchema.shape).sort()).toEqual(await propertyNamesOf('ClientResponseModel'));
});

test('CreateClientRequestObjectSchema matches CreateClientRequestModel', async () => {
  expect(Object.keys(CreateClientRequestObjectSchema.shape).sort()).toEqual(
    await propertyNamesOf('CreateClientRequestModel'),
  );
});

test('ClientListSchema matches ListResultModelOfClientResponseModel', async () => {
  expect(Object.keys(ClientListSchema.shape).sort()).toEqual(
    await propertyNamesOf('ListResultModelOfClientResponseModel'),
  );
});
```

- [ ] **Step 4: Remove the contract test from the frontend**

Run:
```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git rm frontend/vitest.contract.config.ts frontend/contract.setup.ts frontend/src/features/clients/schemas/client-contract.contract.ts"
```
Then in `frontend/package.json` delete the line `"test:contract": "vitest run --config vitest.contract.config.ts",` (line 12).

- [ ] **Step 5: Run the relocated contract suite (spins up the backend)**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter e2e run test:contract"`
Expected: PASS — Docker builds/starts the backend, the three contract assertions pass, teardown runs `docker compose down`.

- [ ] **Step 6: Confirm the frontend still builds/tests without the contract files**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm --filter frontend run typecheck && pnpm --filter frontend test && pnpm --filter frontend run build"`
Expected: PASS.

- [ ] **Step 7: Add the `e2e` solution folder to `Fmis.slnx`**

Open `Fmis.slnx`. It is an XML solution. Add a `<Folder>` element (sibling to the existing project/folder entries) surfacing the e2e config files for IDE navigability:

```xml
  <Folder Name="/e2e/">
    <File Path="e2e/package.json" />
    <File Path="e2e/playwright.config.ts" />
    <File Path="e2e/playwright.contract.config.ts" />
  </Folder>
```

Place it inside the root `<Solution>` element alongside the other top-level entries (match the existing indentation and folder style already present in the file).

- [ ] **Step 8: Verify the solution still loads (build succeeds)**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet build Fmis.slnx"`
Expected: PASS — the `e2e` folder is file/folder-only (no buildable project), the .NET build is unaffected.

- [ ] **Step 9: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A e2e frontend Fmis.slnx pnpm-lock.yaml && git commit -m 'Relocate the contract test into the e2e workspace on Playwright

Rewrites the Zod<->OpenAPI contract test onto Playwrights runner, reusing the
frontend schemas via the workspace dependency and the @/* alias; the backend
is started via docker compose in global setup. Removes the frontend contract
files and surfaces e2e in the solution file.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

---

## Part 4 — CI/CD

### Task 9: Update Main CI for the workspace layout

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: frontend + contract jobs run from the repo root with `pnpm --filter`.

- [ ] **Step 1: Rewrite the `frontend` and `contract` jobs**

In `.github/workflows/ci.yml`, replace the `frontend` job (lines 35-66) and the `contract` job (lines 68-90) with workspace-root versions. The `frontend` job:

```yaml
  frontend:
    name: Frontend
    runs-on: ubuntu-latest
    steps:
      - name: Check out the repository
        uses: actions/checkout@v7

      - name: Set up Node
        uses: actions/setup-node@v6
        with:
          node-version: 24

      - name: Enable Corepack
        run: corepack enable

      - name: Install dependencies
        run: pnpm install --frozen-lockfile

      - name: Lint
        run: pnpm --filter frontend run lint

      - name: Typecheck
        run: pnpm --filter frontend run typecheck

      - name: Test
        run: pnpm --filter frontend test

      - name: Build
        run: pnpm --filter frontend run build
```

The `contract` job (runs the relocated Playwright contract suite; it starts the backend via docker compose in global setup, so no separate compose step is needed; no browser binaries required for API-only tests):

```yaml
  contract:
    name: Contract test
    runs-on: ubuntu-latest
    steps:
      - name: Check out the repository
        uses: actions/checkout@v7

      - name: Set up Node
        uses: actions/setup-node@v6
        with:
          node-version: 24

      - name: Enable Corepack
        run: corepack enable

      - name: Install dependencies
        run: pnpm install --frozen-lockfile

      - name: Contract test
        run: pnpm --filter e2e run test:contract
```

Leave the `backend` job and the `preview` job unchanged.

- [ ] **Step 2: Validate the workflow YAML**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && ruby -ryaml -e \"YAML.load_file('.github/workflows/ci.yml'); puts 'ci.yml OK'\""`
Expected: `ci.yml OK`.

- [ ] **Step 3: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add .github/workflows/ci.yml && git commit -m 'Run CI frontend + contract jobs through the pnpm workspace

Install once at the repo root and target workspaces with pnpm --filter; the
contract job now runs the relocated Playwright contract suite.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 10: CD gate → deploy → verify + conditional environment

**Files:**
- Modify: `.github/workflows/pulumi-stacks.yml` (conditional `environment`; expose outputs)
- Modify: `.github/workflows/cd.yml` (gate → deploy → verify)

**Interfaces:**
- Consumes: the reusable `pulumi-stacks.yml`.
- Produces: CD runs a single `environment`-gated `gate` job, then un-gated `deploy` (reusable workflow) + `verify` (Playwright) jobs.

- [ ] **Step 1: Make the reusable workflow's environment binding conditional**

In `.github/workflows/pulumi-stacks.yml`, add an optional `environment` input and use it for the job's `environment:` (empty string = no environment/no gate). Under `on.workflow_call.inputs`, add after the existing `environment` input's use — actually the existing input is the *target env name* (`dev`). Introduce a separate input controlling the GitHub `environment:` binding. Add to `inputs`:

```yaml
      gated_environment:
        description: GitHub environment to bind for approval; empty to skip the gate.
        required: false
        type: string
        default: ''
```

Then change the job's environment line (line 38) from:

```yaml
    environment: ${{ inputs.environment }}
```

to:

```yaml
    environment: ${{ inputs.gated_environment }}
```

(An empty `environment` value applies no environment and therefore no approval; CI preview will pass `gated_environment: dev` to keep its current behavior, CD will omit it.)

- [ ] **Step 2: Keep CI preview behavior unchanged**

In `.github/workflows/ci.yml`, the `preview` job calls `pulumi-stacks.yml`. Add `gated_environment: dev` to its `with:` block so preview stays gated exactly as today:

```yaml
  preview:
    name: Preview
    needs: [backend]
    uses: ./.github/workflows/pulumi-stacks.yml
    with:
      command: preview
      environment: dev
      gated_environment: dev
    secrets:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
      AUTH0_DOMAIN: ${{ secrets.AUTH0_DOMAIN }}
      AUTH0_CLIENT_ID: ${{ secrets.AUTH0_CLIENT_ID }}
      AUTH0_CLIENT_SECRET: ${{ secrets.AUTH0_CLIENT_SECRET }}
```

- [ ] **Step 3: Rewrite `cd.yml` as gate → deploy → verify**

Replace `.github/workflows/cd.yml` with:

```yaml
name: Main CD

on:
  workflow_run:
    workflows: ["Main CI"]
    types: [completed]
    branches: [main]

permissions:
  id-token: write
  contents: read

jobs:
  gate:
    name: Approve dev
    if: ${{ github.event.workflow_run.conclusion == 'success' && github.event.workflow_run.event == 'push' }}
    runs-on: ubuntu-latest
    environment: dev
    outputs:
      environment: ${{ steps.select.outputs.environment }}
      suite: ${{ steps.select.outputs.suite }}
    steps:
      - name: Select environment and verification suite
        id: select
        run: |
          echo "environment=dev" >> "$GITHUB_OUTPUT"
          echo "suite=e2e" >> "$GITHUB_OUTPUT"

  deploy:
    name: Deploy
    needs: [gate]
    uses: ./.github/workflows/pulumi-stacks.yml
    with:
      command: up
      environment: ${{ needs.gate.outputs.environment }}
    secrets:
      AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
      AZURE_SUBSCRIPTION_ID: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
      AUTH0_DOMAIN: ${{ secrets.AUTH0_DOMAIN }}
      AUTH0_CLIENT_ID: ${{ secrets.AUTH0_CLIENT_ID }}
      AUTH0_CLIENT_SECRET: ${{ secrets.AUTH0_CLIENT_SECRET }}

  verify:
    name: Verify (${{ needs.gate.outputs.suite }})
    needs: [gate, deploy]
    runs-on: ubuntu-latest
    env:
      ENVIRONMENT: ${{ needs.gate.outputs.environment }}
      PULUMI_STATE_ACCOUNT: fmis${{ needs.gate.outputs.environment }}tfstate
      PULUMI_STATE_CONTAINER: pulumi-state
      PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
      AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
    steps:
      - name: Check out the repository
        uses: actions/checkout@v7

      - name: Log in to Azure
        uses: azure/login@v3
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Set up Pulumi
        uses: pulumi/actions@v7
        with:
          pulumi-version: 3.247.0

      - name: Log in to the Pulumi state backend
        run: pulumi login "azblob://${PULUMI_STATE_CONTAINER}?storage_account=${PULUMI_STATE_ACCOUNT}"

      - name: Read stack outputs
        run: |
          cd infra/application
          pulumi stack select "${ENVIRONMENT}"
          echo "E2E_FRONTEND_URL=$(pulumi stack output frontendUrl)" >> "$GITHUB_ENV"
          echo "E2E_BACKEND_URL=$(pulumi stack output backendUrl)" >> "$GITHUB_ENV"
          echo "E2E_SPA_CLIENT_ID=$(pulumi stack output spaClientId)" >> "$GITHUB_ENV"
          cd ../auth
          pulumi stack select "${ENVIRONMENT}"
          echo "::add-mask::$(pulumi stack output e2eClientSecret --show-secrets)"
          echo "::add-mask::$(pulumi stack output e2ePassword --show-secrets)"
          {
            echo "E2E_AUTH_DOMAIN=$(pulumi stack output domain)"
            echo "E2E_AUTH_AUDIENCE=$(pulumi stack output audience)"
            echo "E2E_CLIENT_ID=$(pulumi stack output e2eClientId --show-secrets)"
            echo "E2E_CLIENT_SECRET=$(pulumi stack output e2eClientSecret --show-secrets)"
            echo "E2E_USERNAME=$(pulumi stack output e2eUsername --show-secrets)"
            echo "E2E_PASSWORD=$(pulumi stack output e2ePassword --show-secrets)"
          } >> "$GITHUB_ENV"

      - name: Set up Node
        uses: actions/setup-node@v6
        with:
          node-version: 24

      - name: Enable Corepack
        run: corepack enable

      - name: Install dependencies
        run: pnpm install --frozen-lockfile

      - name: Install Playwright browsers
        run: pnpm --filter e2e exec playwright install --with-deps chromium

      - name: Run ${{ needs.gate.outputs.suite }} suite
        run: pnpm --filter e2e run test:${{ needs.gate.outputs.suite }}
```

Notes for the implementer:
- The application stack must publish `spaClientId` as an output for the verify job. Add it (see Step 4).
- `deploy` and `verify` have no `environment:` — they authenticate via the branch-scoped federated credential (operator prerequisite, Task 12).
- Secrets are read from Pulumi state at runtime and masked with `::add-mask::` before being written to `$GITHUB_ENV`.

- [ ] **Step 4: Publish `spaClientId` from the application stack**

The verify job reads `pulumi stack output spaClientId` from the application stack. Add the output. In `infra/application/ApplicationStack.cs`, add the property near the other outputs (lines 10-11):

```csharp
    [Output("spaClientId")] public Output<string> SpaClientId { get; private set; }
```

and set it at the end of the constructor (near `BackendUrl = ...`):

```csharp
        SpaClientId = spaClient.ClientId;
```

Add a test to `ApplicationStackTests.cs`:

```csharp
    [Fact]
    public async Task Exposes_the_spa_client_id_output()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();
        var stack = resources.OfType<Fmis.Infra.Application.ApplicationStack>().Single();

        Assert.NotNull(await InfraTesting.GetAsync(stack.SpaClientId));
    }
```

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter 'FullyQualifiedName~Fmis.Infra.Tests.ApplicationStackTests'"`
Expected: PASS.

- [ ] **Step 5: Validate the workflow YAML**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && for f in ci cd pulumi-stacks; do ruby -ryaml -e \"YAML.load_file('.github/workflows/\$f.yml'); puts '\$f OK'\"; done"`
Expected: `ci OK`, `cd OK`, `pulumi-stacks OK`.

- [ ] **Step 6: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add -A .github/workflows infra/application infra/tests && git commit -m 'CD gate -> deploy -> verify with a single approval

A gate job carries environment: dev (the only approval) and emits the env name
+ verify suite; deploy and verify run un-gated off its outputs. Verify reads
the deployed URLs and e2e creds from Pulumi state (masked) and runs the e2e
suite on dev. The reusable workflow environment binding is now conditional so
CI preview is undisturbed. The application stack publishes spaClientId.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 11: Operator runbook + roadmap update

**Files:**
- Create: `docs/conventions/cd-federated-credentials.md`
- Modify: `docs/conventions/architecture.md` (roadmap line)

**Interfaces:**
- Produces: documentation only; no code.

- [ ] **Step 1: Document the branch-scoped federated credential prerequisite**

Create `docs/conventions/cd-federated-credentials.md`:

```markdown
# CD federated credentials

Main CD runs `gate` (with `environment: dev` for the single required-reviewer
approval) followed by `deploy` and `verify`, which do **not** carry an
`environment`. Their GitHub OIDC token subject is therefore the branch ref, not
`environment:dev`, so Azure needs a federated credential matching it.

## Required (one-time, per Azure app registration)

Add a federated credential to the CD service principal's app registration:

- Issuer: `https://token.actions.githubusercontent.com`
- Subject: `repo:uplift-klinker/modern-fmis:ref:refs/heads/main`
- Audience: `api://AzureADTokenExchange`

The existing `environment:dev` federated credential stays for CI preview.

## Security note

A branch-scoped credential means the `gate` job enforces **approval and
ordering**, not a hard Azure-credential boundary: any workflow running on the
protected `main` branch can obtain these credentials. This is an accepted
trade-off for single-approval automatic post-deploy verification on a protected
`main`.
```

- [ ] **Step 2: Update the roadmap line**

In `docs/conventions/architecture.md`, find the roadmap line referencing the Playwright smoke E2E (the line reading `Cross-stack: a Playwright smoke E2E (incl. Auth0 login) and the Zod↔OpenAPI contract test.`). Append a status marker so the roadmap reflects Phase 3c delivery:

Change it to:
```
Cross-stack: a Playwright smoke E2E (incl. Auth0 login) and the Zod↔OpenAPI contract test. **(Phase 3c — delivered: e2e/ workspace, gate→deploy→verify CD.)**
```

- [ ] **Step 3: Commit**

```bash
zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && git add docs/conventions && git commit -m 'Document CD federated credentials and mark Phase 3c on the roadmap

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>'"
```

### Task 12: Full-suite verification + final review

**Files:** none (verification only).

- [ ] **Step 1: Run the whole .NET solution test suite**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx"`
Expected: PASS — all backend + infra tests green.

- [ ] **Step 2: Run the frontend + contract suites through the workspace**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && pnpm install --frozen-lockfile && pnpm --filter frontend run lint && pnpm --filter frontend run typecheck && pnpm --filter frontend test && pnpm --filter frontend run build && pnpm --filter e2e run typecheck && pnpm --filter e2e run lint && pnpm --filter e2e run test:contract"`
Expected: PASS.

- [ ] **Step 3: Confirm the system suite is discoverable**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && E2E_FRONTEND_URL=http://placeholder pnpm --filter e2e exec playwright test --config playwright.config.ts --list"`
Expected: PASS — 3 tests listed, one `@smoke`.

- [ ] **Step 4: Validate all workflow YAML**

Run: `zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && for f in ci cd pulumi-stacks; do ruby -ryaml -e \"YAML.load_file('.github/workflows/\$f.yml'); puts '\$f OK'\"; done"`
Expected: all OK.

- [ ] **Step 5: Runtime verification note (post-merge)**

The system suite (`test:e2e`) and the gate→deploy→verify pipeline can only be exercised against deployed dev. After merge, the first Main CD run: approve the `gate` job once, watch `deploy` complete, and `verify` run the e2e suite. If the interactive-login selectors (`interactiveLogin`) or the `seedAuthSession` cache format need adjustment, they are each isolated to a single helper in `e2e/support/auth.ts`. Confirm the operator added the branch-scoped federated credential (Task 11) before this run.

---

## Notes for the executor

- **TDD discipline:** every infra task writes/changes the test first, sees it fail, then implements. The contract suite is runnable locally (docker). The system suite is not runnable pre-deploy — verify by `--list` + typecheck + lint, per the Global Constraints.
- **Load-bearing contracts verified against installed packages:** `Auth0.AttackProtection` (3.45.0) nested args with `Enabled`; `@auth0/auth0-spa-js` 2.21.1 cache key `@@auth0spajs@@::<clientId>::<audience>::<scope>` and `{ body, expiresAt }` shape; `Auth0.Client` `Callbacks`/`AllowedLogoutUrls`/`WebOrigins`; `StackReference` mock shape.
- **Order matters:** Task 3 depends on Task 1 (auth no longer emits `spaClientId`). Tasks 4→5→6→7→8 are the workspace build-up. Task 10 depends on Task 8 (e2e scripts exist) and adds the `spaClientId` application output the verify job reads.
