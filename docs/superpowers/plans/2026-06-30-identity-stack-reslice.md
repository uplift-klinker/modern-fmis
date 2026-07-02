# Identity Stack Re-slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the ACR into the deletion-protected `persistence` tier, extract a dedicated `identity` stack that owns the managed identity + all its permissions, and rework `application` to push via the deploy principal's `AcrPush` with ACR admin disabled.

**Architecture:** Four stacks — `auth` → `persistence` (server + db + ACR) → `identity` (UAMI + AcrPull/AcrPush + Postgres grant) → `application` (Container App + frontend, consuming the others). The identity + its Postgres authorization move out of `persistence`; the ACR moves in.

**Tech Stack:** Pulumi C# + `Pulumi.AzureNative` 3.19.0 (ACR, Container Apps, Storage, Authorization) + `Pulumi.PostgreSql` + `Pulumi.Command` + `Azure.Identity` + `Pulumi.DockerBuild` 0.0.20 + `Pulumi.SyncedFolder` 0.12.4; `Pulumi.Testing` + xUnit.

## Global Constraints

- **No code comments** — self-documenting names. **Exact pinned package versions.** **TDD** (red before green). **New commits only.**
- **ComponentResource composition**, sealed, type token `fmis:<layer>:<Type>`, children parented via `new CustomResourceOptions { Parent = this }`, `RegisterOutputs()`; thin stack roots.
- **Naming** `ResourceNames.For(env, layer, resource)`; default region `centralus`; run `dotnet` from the repo root; shell `zsh -lc`.
- **Version-sensitive** AzureNative / role-definition-ids / StackReference-mock — verify against installed packages; **the test assertions are the load-bearing contract.**
- Role-definition ids: **AcrPull** `7f951dda-4ed3-4680-a7ca-43fe172d538d`, **AcrPush** `8311e382-0749-4cb8-b61a-304f252e45ec`, prefixed `/providers/Microsoft.Authorization/roleDefinitions/`.

---

## File structure

```
infra/persistence/Components/ContainerRegistry.cs   # NEW (ACR, admin off, protected)
infra/persistence/Components/DatabaseIdentity.cs    # DELETE (moves to identity)
infra/persistence/PostgresAdminToken.cs             # MOVE → infra/identity/
infra/persistence/PersistenceStack.cs               # + registry, − identity, + acr outputs
infra/identity/                                     # NEW project (mirror infra/persistence)
  Fmis.Infra.Identity.csproj · Pulumi.yaml · Pulumi.dev.yaml · Program.cs · IdentityStack.cs
  PostgresAdminToken.cs · Components/{AppIdentity,RegistryAccess,DatabaseAccess}.cs
infra/application/Components/ContainerRegistry.cs   # DELETE (ACR now in persistence)
infra/application/ApplicationStack.cs               # ref persistence+identity; drop ListRegistryCredentials
infra/application/Components/BackendApp.cs           # use identity resourceId; drop AcrPull RoleAssignment
infra/tests/Fmis.Infra.Tests/StackMocks.cs           # persistence/identity/auth stack-ref outputs
infra/tests/Fmis.Infra.Tests/InfraTesting.cs         # RunIdentityStackAsync; trim RunPersistenceStackAsync
infra/tests/Fmis.Infra.Tests/{Persistence,Identity,Application}StackTests.cs
.github/workflows/infra-deploy.yml                   # + identity deploy
.github/workflows/cd.yml                             # apps job + az acr login
Fmis.slnx                                            # + the identity project
```

**Ordering note for reviewers:** Parts run in order. Part 1 leaves `persistence` green but with the identity removed; the `application` suite is temporarily broken between Part 1 and Part 3 (it still references the old persistence `appIdentity*` / its own ACR) — that's expected and resolved in Part 3. Run the **full** `dotnet test Fmis.slnx` only at the end of Part 3; within Parts 1–2 use the per-suite filters shown.

---

# PART 1 — Persistence rework

## Task 1: Add the ACR to persistence (deletion-protected)

**Files:** Create `infra/persistence/Components/ContainerRegistry.cs`; Modify `infra/persistence/PersistenceStack.cs`, `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`.

**Interfaces:** Produces `ContainerRegistry` (persistence) exposing `Output<string> LoginServer`, `Output<string> RegistryId`, `AzureNative.ContainerRegistry.Registry Registry`; `PersistenceStack` gains `[Output("acrLoginServer")] AcrLoginServer`, `[Output("acrId")] AcrId`, `[Output("acrName")] AcrName`.

- [ ] **Step 1: Failing test** — add to `PersistenceStackTests`:
```csharp
[Fact]
public async Task Creates_a_deletion_protected_container_registry()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    var registry = resources.OfType<AzureNative.ContainerRegistry.Registry>().Single();
    Assert.Equal("Basic", await InfraTesting.GetAsync(registry.Sku.Apply(s => s.Name)));
    Assert.Contains(resources.OfType<AzureNative.Authorization.ManagementLockByScope>(),
        l => InfraTesting.GetAsync(l.Level).Result == "CanNotDelete"
            && InfraTesting.GetAsync(l.LockName).Result.Contains("acr"));
}
```
The persistence suite already imports `AzureNative`. (There are now two `ManagementLockByScope` — the server's and the ACR's — so the server lock test must also still pass; the ACR lock is matched by its `acr` name.)

- [ ] **Step 2: Run red** → `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/.worktrees/application-stack && dotnet test Fmis.slnx --filter "FullyQualifiedName~PersistenceStackTests"'` → FAIL.

- [ ] **Step 3: Implement** — `infra/persistence/Components/ContainerRegistry.cs` (this mirrors the ACR component that currently lives in `infra/application` — copy that file's proven code, change the namespace to `Fmis.Infra.Persistence.Components`, add the deletion protection). Sealed, token `fmis:persistence:ContainerRegistry`:
```csharp
using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Persistence.Components;

public sealed class ContainerRegistry : ComponentResource
{
    public Output<string> LoginServer { get; }
    public Output<string> RegistryId { get; }
    public AzureNative.ContainerRegistry.Registry Registry { get; }

    public ContainerRegistry(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:persistence:ContainerRegistry", name, options)
    {
        Registry = new AzureNative.ContainerRegistry.Registry(name, new AzureNative.ContainerRegistry.RegistryArgs
        {
            ResourceGroupName = resourceGroupName,
            RegistryName = name,
            Location = location,
            Sku = new AzureNative.ContainerRegistry.Inputs.SkuArgs { Name = AzureNative.ContainerRegistry.SkuName.Basic },
            AdminUserEnabled = false,
        }, new CustomResourceOptions { Parent = this, Protect = true });

        _ = new AzureNative.Authorization.ManagementLockByScope($"{name}-lock", new AzureNative.Authorization.ManagementLockByScopeArgs
        {
            Scope = Registry.Id,
            LockName = $"{name}-acr-cannotdelete",
            Level = AzureNative.Authorization.LockLevel.CanNotDelete,
        }, new CustomResourceOptions { Parent = this });

        LoginServer = Registry.LoginServer;
        RegistryId = Registry.Id;
        RegisterOutputs();
    }
}
```
In `PersistenceStack.cs`, after the server, construct the registry (compacted name `fmis{env}acr`) and add the outputs:
```csharp
var registry = new ContainerRegistry($"fmis{env}acr", resourceGroup.Name, location);
```
Add `[Output("acrLoginServer")] public Output<string> AcrLoginServer { get; private set; }`, `[Output("acrId")] AcrId`, `[Output("acrName")] AcrName` and assign `AcrLoginServer = registry.LoginServer; AcrId = registry.RegistryId; AcrName = Output.Create($"fmis{env}acr");`. Add a `StackMocks` name mapping for `registryName → name` if any test asserts the registry `.Name` (the lock test uses `LockName`, so likely unneeded). Verify `SkuName.Basic`/`Sku.Name` (a non-nullable `string` field in 3.19.0 — no `!`).

- [ ] **Step 4: Run green** → the filter passes. **Commit** `git add infra && git commit -m "Add the deletion-protected ACR to the persistence tier (TDD)"` (+ Co-Authored-By trailer).

## Task 2: Remove the managed identity from persistence

**Files:** Delete `infra/persistence/Components/DatabaseIdentity.cs`; Move `infra/persistence/PostgresAdminToken.cs` → (removed here, re-created in Task 3); Modify `infra/persistence/PersistenceStack.cs`, `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`, `infra/tests/Fmis.Infra.Tests/InfraTesting.cs`.

- [ ] **Step 1: Remove the failing-if-present assertions.** In `PersistenceStackTests`, delete the tests that assert the managed identity / the `pgaadauth` command / the postgresql grants (e.g. `Creates_the_app_managed_identity`, `Provisions_the_entra_principal_and_grants`) — the identity is no longer persistence's responsibility. Keep the server/db/firewall/PostGIS/admin/lock/ACR tests + the outputs test (minus `appIdentity*`).

- [ ] **Step 2: Implement the removal** — in `PersistenceStack.cs`, delete the `DatabaseIdentity` construction and the `appIdentityClientId`/`appIdentityPrincipalId`/`appIdentityName` `[Output]` properties + assignments. Delete `infra/persistence/Components/DatabaseIdentity.cs`. Delete `infra/persistence/PostgresAdminToken.cs` (it is re-created under `infra/identity` in Task 3). **Keep** `PostgresServer` intact (server + both firewall rules + PostGIS config + database + Entra admin + lock) — the identity stack's data-plane auth connects through the server's deployer-IP firewall rule. In `InfraTesting.RunPersistenceStackAsync`, remove the `Fmis.Infra.Persistence.PostgresAdminToken.Provider` override + restore (persistence no longer references it); keep the `DEPLOYER_IP`/`DEPLOY_PRINCIPAL_OBJECT_ID`/`AZURE_TENANT_ID` env set+restore (the server still reads them).

- [ ] **Step 3: Build + run** → `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/.worktrees/application-stack && dotnet build infra/persistence/Fmis.Infra.Persistence.csproj && dotnet test Fmis.slnx --filter "FullyQualifiedName~PersistenceStackTests"'` → green. (The `application`/its tests will not compile against the removed persistence outputs yet — that is expected and fixed in Part 3; do not run the full suite here. If the test project fails to build because `RunApplicationStackAsync`/`ApplicationStackTests` reference removed persistence members, that is Part 3's scope — for this task, confirm the persistence project builds and its own tests pass by building the persistence + Common projects and running the persistence filter.)

- [ ] **Step 4: Commit** `git add infra && git commit -m "Remove the managed identity + Postgres grant from persistence (moves to identity) (TDD)"`.

---

# PART 2 — The `identity` stack

## Task 3: Scaffold infra/identity + the token seam

**Files:** Create `infra/identity/{Fmis.Infra.Identity.csproj,Pulumi.yaml,Program.cs,IdentityStack.cs,PostgresAdminToken.cs}`; Modify `Fmis.slnx`, `infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj`.

- [ ] **Step 1: Create + packages + refs** (mirror `infra/persistence`; pin resolved versions; `/infra/` solution folder):
```bash
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/.worktrees/application-stack/infra && mkdir -p identity && cd identity && dotnet new console -n Fmis.Infra.Identity -o . && rm -f Program.cs'
for pkg in Pulumi Pulumi.AzureNative Pulumi.PostgreSql Pulumi.Command Azure.Identity; do zsh -lc "cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/.worktrees/application-stack/infra/identity && dotnet add Fmis.Infra.Identity.csproj package $pkg"; done
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/.worktrees/application-stack/infra/identity && dotnet add Fmis.Infra.Identity.csproj reference ../Fmis.Infra.Common/Fmis.Infra.Common.csproj'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/.worktrees/application-stack && dotnet sln Fmis.slnx add infra/identity/Fmis.Infra.Identity.csproj && dotnet add infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj reference infra/identity/Fmis.Infra.Identity.csproj'
```
Trim inherited props to match `infra/persistence`; move into the `/infra/` solution folder. `Pulumi.yaml`: `name: fmis-identity`, `runtime: dotnet`, a description. `IdentityStack.cs`: `public class IdentityStack : Stack { public IdentityStack() { } }`. `Program.cs`: `return await Pulumi.Deployment.RunAsync<Fmis.Infra.Identity.IdentityStack>();`.

- [ ] **Step 2: Re-create the token seam** — `infra/identity/PostgresAdminToken.cs` (identical to the deleted persistence one but namespace `Fmis.Infra.Identity`):
```csharp
using Azure.Core;
using Azure.Identity;
using Pulumi;

namespace Fmis.Infra.Identity;

public static class PostgresAdminToken
{
    public static Func<Output<string>> Provider { get; set; } = FromAzureIdentity;

    private static Output<string> FromAzureIdentity() => Output.CreateSecret(Output.Create(FetchAsync()));

    private static async Task<string> FetchAsync()
    {
        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" }));
        return token.Token;
    }
}
```

- [ ] **Step 3: Build** → `dotnet build Fmis.slnx` (persistence + identity compile; application-test compile errors from Part 1 are still expected — build just the identity + persistence + Common projects to confirm). **Commit** `git add infra Fmis.slnx && git commit -m "Scaffold infra/identity project + move the Postgres token seam"`.

## Task 4: Identity test harness + persistence stack-ref mock

**Files:** Modify `infra/tests/Fmis.Infra.Tests/StackMocks.cs`, `infra/tests/Fmis.Infra.Tests/InfraTesting.cs`.

**Interfaces:** Produces `InfraTesting.RunIdentityStackAsync() : Task<ImmutableArray<Resource>>`; `StackMocks` stack-reference branch returns `auth` / `persistence` / `identity` outputs by referenced name.

- [ ] **Step 1: Update the StackReference mock** — in `StackMocks.NewResourceAsync`'s `pulumi:pulumi:StackReference` branch, key on `args.Name` for three cases (persistence now emits ACR + server, no `appIdentity*`; identity emits `appIdentity*`):
```csharp
Dictionary<string, object> outputs;
if (args.Name.Contains("auth"))
    outputs = new() { ["domain"] = "fmis-dev.us.auth0.com", ["spaClientId"] = "spa-client-id", ["audience"] = "https://dev.api.modern-fmis" };
else if (args.Name.Contains("identity"))
    outputs = new()
    {
        ["appIdentityClientId"] = "00000000-0000-0000-0000-000000000001",
        ["appIdentityPrincipalId"] = "00000000-0000-0000-0000-000000000002",
        ["appIdentityName"] = "fmis-dev-app-identity",
        ["appIdentityResourceId"] = "/subscriptions/sub/resourceGroups/fmis-dev-identity-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/fmis-dev-app-identity",
    };
else
    outputs = new()
    {
        ["serverFqdn"] = "fmis-dev-persistence-postgres.postgres.database.azure.com",
        ["databaseName"] = "fmis",
        ["acrLoginServer"] = "fmisdevacr.azurecr.io",
        ["acrId"] = "/subscriptions/sub/resourceGroups/fmis-dev-persistence-rg/providers/Microsoft.ContainerRegistry/registries/fmisdevacr",
    };
```
Keep the existing `secretOutputNames = ImmutableArray<string>.Empty` + `outputs` state assignment.

- [ ] **Step 2: Add `RunIdentityStackAsync`** to `InfraTesting` (mirror the OLD `RunPersistenceStackAsync` that had the token seam — it sets `DEPLOYER_IP`/`DEPLOY_PRINCIPAL_OBJECT_ID`/`AZURE_TENANT_ID`, overrides `Fmis.Infra.Identity.PostgresAdminToken.Provider = () => Output.CreateSecret("test-token")`, runs `Deployment.TestAsync<Fmis.Infra.Identity.IdentityStack>(new StackMocks(), new TestOptions { StackName="dev", ProjectName="fmis-identity", IsPreview=false })`, restores env + provider in `finally`).

- [ ] **Step 3: Build** → identity + tests compile (application-test errors still expected until Part 3). **Commit.**

## Task 5: AppIdentity component + stack root

**Files:** Create `infra/identity/Components/AppIdentity.cs`, `infra/tests/Fmis.Infra.Tests/IdentityStackTests.cs`; Modify `infra/identity/IdentityStack.cs`.

**Interfaces:** Produces `AppIdentity` exposing `Output<string> ClientId, PrincipalId, Name, ResourceId`.

- [ ] **Step 1: Failing test** — `IdentityStackTests.cs`:
```csharp
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Tests;

public class IdentityStackTests
{
    [Fact]
    public async Task Creates_the_app_managed_identity()
    {
        var resources = await InfraTesting.RunIdentityStackAsync();

        var identity = resources.OfType<AzureNative.ManagedIdentity.UserAssignedIdentity>().Single();
        Assert.Equal("fmis-dev-app-identity", await InfraTesting.GetAsync(identity.Name));
    }
}
```

- [ ] **Step 2: Run red** → `dotnet test Fmis.slnx --filter "FullyQualifiedName~IdentityStackTests"` → FAIL.

- [ ] **Step 3: Implement** `infra/identity/Components/AppIdentity.cs` (this is the UAMI half of the old `DatabaseIdentity`):
```csharp
using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Identity.Components;

public sealed class AppIdentity : ComponentResource
{
    public Output<string> ClientId { get; }
    public Output<string> PrincipalId { get; }
    public Output<string> Name { get; }
    public Output<string> ResourceId { get; }

    public AppIdentity(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:identity:AppIdentity", name, options)
    {
        var identity = new AzureNative.ManagedIdentity.UserAssignedIdentity(name, new AzureNative.ManagedIdentity.UserAssignedIdentityArgs
        {
            ResourceGroupName = resourceGroupName,
            ResourceName = name,
            Location = location,
        }, new CustomResourceOptions { Parent = this });

        ClientId = identity.ClientId;
        PrincipalId = identity.PrincipalId;
        Name = Output.Create(name);
        ResourceId = identity.Id;
        RegisterOutputs();
    }
}
```
In `IdentityStack.cs`, read `env`, create the resource group (`ResourceNames.For(env,"identity","rg")`), the persistence stack reference (`new StackReference("persistence", new StackReferenceArgs { Name = $"fmis-persistence/{env}" })`), and `AppIdentity` (`ResourceNames.For(env,"identity","identity")` → `fmis-dev-identity-identity`? — use `ResourceNames.For(env,"app","identity")` to keep the name `fmis-dev-app-identity` matching the old identity). The `StackMocks` maps `resourceName → name`, so `identity.Name` resolves.

- [ ] **Step 4: Run green** (filter). **Commit.**

## Task 6: RegistryAccess (AcrPull + AcrPush)

**Files:** Create `infra/identity/Components/RegistryAccess.cs`; Modify `infra/identity/IdentityStack.cs`, `IdentityStackTests.cs`.

**Interfaces:** Consumes the ACR scope (`acrId` from persistence), the identity `principalId`, the deploy principal object id (env). Produces two `RoleAssignment`s.

- [ ] **Step 1: Failing test** — add:
```csharp
[Fact]
public async Task Grants_the_identity_pull_and_the_deployer_push_on_the_acr()
{
    var resources = await InfraTesting.RunIdentityStackAsync();

    var roles = resources.OfType<AzureNative.Authorization.RoleAssignment>().ToList();
    Assert.Equal(2, roles.Count);
    var roleDefs = await Task.WhenAll(roles.Select(r => InfraTesting.GetAsync(r.RoleDefinitionId)));
    Assert.Contains(roleDefs, d => d.Contains("7f951dda-4ed3-4680-a7ca-43fe172d538d"));
    Assert.Contains(roleDefs, d => d.Contains("8311e382-0749-4cb8-b61a-304f252e45ec"));
}
```

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** `infra/identity/Components/RegistryAccess.cs` (deterministic role-assignment names, same SHA256-first-16 helper used in the application `BackendApp`):
```csharp
using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Identity.Components;

public sealed class RegistryAccess : ComponentResource
{
    private const string AcrPull = "/providers/Microsoft.Authorization/roleDefinitions/7f951dda-4ed3-4680-a7ca-43fe172d538d";
    private const string AcrPush = "/providers/Microsoft.Authorization/roleDefinitions/8311e382-0749-4cb8-b61a-304f252e45ec";

    public RegistryAccess(string name, Input<string> acrId, Input<string> identityPrincipalId, Input<string> deployerPrincipalId, ComponentResourceOptions? options = null)
        : base("fmis:identity:RegistryAccess", name, options)
    {
        Assign($"{name}-pull", acrId, AcrPull, identityPrincipalId, AzureNative.Authorization.PrincipalType.ServicePrincipal);
        Assign($"{name}-push", acrId, AcrPush, deployerPrincipalId, AzureNative.Authorization.PrincipalType.ServicePrincipal);
        RegisterOutputs();
    }

    private void Assign(string name, Input<string> scope, string roleDefinitionId, Input<string> principalId, AzureNative.Authorization.PrincipalType principalType)
    {
        var assignmentName = Output.Tuple(scope.ToOutput(), principalId.ToOutput())
            .Apply(t => DeterministicName(t.Item1, t.Item2, roleDefinitionId));
        _ = new AzureNative.Authorization.RoleAssignment(name, new AzureNative.Authorization.RoleAssignmentArgs
        {
            RoleAssignmentName = assignmentName,
            RoleDefinitionId = roleDefinitionId,
            PrincipalId = principalId,
            PrincipalType = principalType,
            Scope = scope,
        }, new CustomResourceOptions { Parent = this });
    }

    private static string DeterministicName(string scope, string principalId, string role)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{scope}|{principalId}|{role}"));
        return new Guid(hash[..16]).ToString();
    }
}
```
In `IdentityStack.cs`, read `acrId` from the persistence reference + `DEPLOY_PRINCIPAL_OBJECT_ID` from env, and construct `new RegistryAccess("fmis-<env>-registry-access", persistence.GetOutput("acrId").Apply(v => v!.ToString()!), appIdentity.PrincipalId, deployerObjectId)`. Verify `Input<T>.ToOutput()` exists (it does in Pulumi .NET); adjust if the tuple form differs. Verify AcrPush role-definition id.

- [ ] **Step 4: Run green** (filter). **Commit.**

## Task 7: DatabaseAccess (pgaadauth principal + grants)

**Files:** Create `infra/identity/Components/DatabaseAccess.cs`; Modify `infra/identity/IdentityStack.cs`, `IdentityStackTests.cs`.

**Interfaces:** Consumes the persistence `serverFqdn`/`databaseName`, the identity `Name`, the admin token seam, the deployer-IP firewall dependency. Produces the `pgaadauth` `command` + the postgresql `Grant`(s).

- [ ] **Step 1: Failing test** — add (this mirrors the persistence test that moved here):
```csharp
[Fact]
public async Task Provisions_the_entra_principal_and_grants()
{
    var resources = await InfraTesting.RunIdentityStackAsync();

    Assert.NotEmpty(resources.OfType<Pulumi.Command.Local.Command>());
    var grant = resources.OfType<Pulumi.PostgreSql.Grant>().Single();
    Assert.Equal("fmis", await InfraTesting.GetAsync(grant.Database));
}
```

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** — move the pgaadauth-command + postgresql-provider + Grant logic from the deleted persistence `DatabaseIdentity` into `infra/identity/Components/DatabaseAccess.cs` (sealed, token `fmis:identity:DatabaseAccess`). Constructor `(string name, Input<string> serverFqdn, Input<string> databaseName, Input<string> identityName, string entraAdminLogin, Output<string> adminToken, ComponentResourceOptions? options = null)`. It creates the `pulumi-postgresql` Provider (Host=serverFqdn, Username=entraAdminLogin, Password=adminToken, Sslmode=require), the `Pulumi.Command.Local.Command` running `pgaadauth_create_principal('<identityName>', false, false)`, and the `Pulumi.PostgreSql.Grant` on the `fmis` db — reuse the exact working code from the persistence `DatabaseIdentity` (copy before deleting in Task 2 if needed via git history: `git show HEAD~:infra/persistence/Components/DatabaseIdentity.cs`). In `IdentityStack.cs`, construct it with `entraAdminLogin = "fmis-ci-deployer"` (the persistence Entra admin login) and `adminToken = PostgresAdminToken.Provider()`, reading `serverFqdn`/`databaseName` from the persistence reference. (No deployer-IP firewall rule here — it lives in `persistence`'s server; the identity stack connects through it at deploy time.)

- [ ] **Step 4: Run green** (filter + `~IdentityStackTests`). **Commit.**

## Task 8: Identity outputs + dev config

**Files:** Modify `infra/identity/IdentityStack.cs`; Create `infra/identity/Pulumi.dev.yaml`; Modify `IdentityStackTests.cs`.

- [ ] **Step 1: Failing test**:
```csharp
[Fact]
public async Task Exposes_the_identity_outputs()
{
    var resources = await InfraTesting.RunIdentityStackAsync();
    var stack = resources.OfType<Fmis.Infra.Identity.IdentityStack>().Single();

    Assert.Equal("fmis-dev-app-identity", await InfraTesting.GetAsync(stack.AppIdentityName));
    Assert.NotNull(await InfraTesting.GetAsync(stack.AppIdentityResourceId));
}
```

- [ ] **Step 2: Run red** → FAIL. **Step 3: Implement** — add `[Output("appIdentityClientId")]`, `[Output("appIdentityPrincipalId")]`, `[Output("appIdentityName")]`, `[Output("appIdentityResourceId")]` to `IdentityStack`, assigned from `appIdentity`. Create `infra/identity/Pulumi.dev.yaml` with `config: {}`. **Step 4: Run green** (filter). **Commit.**

---

# PART 3 — Application rework

## Task 9: Consume the external ACR + identity

**Files:** Delete `infra/application/Components/ContainerRegistry.cs`; Modify `infra/application/ApplicationStack.cs`, `infra/application/Components/BackendApp.cs`, `infra/tests/Fmis.Infra.Tests/ApplicationStackTests.cs`.

- [ ] **Step 1: Update the tests** — in `ApplicationStackTests`, delete `Creates_a_basic_container_registry` (the ACR is no longer in this stack). Keep the backend/env/image/frontend/outputs tests; the image test still asserts the app image contains `fmis-backend`. The env test still asserts the connection string + Auth0 + `AZURE_CLIENT_ID` (now sourced from the identity reference — the mock supplies `appIdentityClientId`).

- [ ] **Step 2: Run red** → `dotnet test Fmis.slnx --filter "FullyQualifiedName~ApplicationStackTests"` → FAIL/compile-error (the stack still references the removed local ACR + persistence `appIdentity*`).

- [ ] **Step 3: Implement** — in `ApplicationStack.cs`: delete the local `ContainerRegistry` construction, the `ListRegistryCredentials.Invoke`, and the credentials in the image `Registries`. Delete `infra/application/Components/ContainerRegistry.cs`. Add stack references:
```csharp
var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"fmis-persistence/{env}" });
var identity = new StackReference("identity", new StackReferenceArgs { Name = $"fmis-identity/{env}" });
var acrLoginServer = persistence.GetOutput("acrLoginServer").Apply(v => v!.ToString()!);
```
The image tag becomes `Output.Format($"{acrLoginServer}/fmis-backend:latest")`; the `DockerBuild.Image` `Registries` has **only** `Address = acrLoginServer` (no Username/Password). Keep `Push = true` and the app's `DependsOn = { image }`. `BackendApp` now takes `identityResourceId` = `identity.GetOutput("appIdentityResourceId")…`, `identityClientId`/`identityPrincipalId`/`identityName` from the identity reference, `serverFqdn`/`databaseName` from persistence, `acrLoginServer` from persistence — and **no longer creates the AcrPull `RoleAssignment`** (delete that block + the `AcrPullRoleDefinitionId`/`DeterministicRoleAssignmentName` members from `BackendApp.cs`; the app just references the identity, whose pull grant lives in the identity stack). Remove the `GetClientConfig`-based identity-resource-id construction (identity emits `appIdentityResourceId` directly). Update the `StackMocks` stack-ref branch is already handled (Task 4). The `Registries` entry on the ContainerApp (`RegistryCredentialsArgs { Server = acrLoginServer, Identity = identityResourceId }`) stays (runtime pull via the identity).

- [ ] **Step 4: Run green** → the filter passes; then the FULL suite `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/.worktrees/application-stack && dotnet test Fmis.slnx'` → **all green** (backend + persistence + identity + application). **Commit** `git add infra && git commit -m "Rework application to consume the external ACR + identity (admin-off push) (TDD)"`.

---

# PART 4 — CD

## Task 10: Deploy identity; app-side az acr login

**Files:** Modify `.github/workflows/infra-deploy.yml`, `.github/workflows/cd.yml`.

- [ ] **Step 1: `infra-deploy.yml`** — the `Compute persistence deploy inputs` step already exports `DEPLOYER_IP` + `DEPLOY_PRINCIPAL_OBJECT_ID`; ensure it runs **before both** the persistence and identity stacks (move it up if needed so identity sees them). After the persistence preview/up steps, add identity steps mirroring persistence (working-directory `infra/identity`), gated the same way (`if: !inputs.deploy` preview / `if: inputs.deploy` up), with `env: PULUMI_CONFIG_PASSPHRASE + AZURE_TENANT_ID`:
```yaml
      - name: Select or init the identity stack
        working-directory: infra/identity
        run: pulumi stack select ${{ inputs.environment }} || pulumi stack init ${{ inputs.environment }}
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
      - name: Identity preview
        if: ${{ !inputs.deploy }}
        working-directory: infra/identity
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
          AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
        run: pulumi preview
      - name: Identity up
        if: ${{ inputs.deploy }}
        working-directory: infra/identity
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
          AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}
        run: pulumi up --yes
```

- [ ] **Step 2: `cd.yml` apps job** — add, after `azure/login` and before `Pulumi login`/`Application up`, a step:
```yaml
      - name: ACR login
        run: az acr login --name fmis${ENVIRONMENT}acr
```
(The deploy principal has `AcrPush` from the identity stack, and the ACR pre-exists in `persistence`, so the Pulumi `docker-build` push uses the ambient docker credential — ACR admin stays disabled.)

- [ ] **Step 3: Validate** → `ruby -ryaml -e 'YAML.load_file(".github/workflows/infra-deploy.yml"); YAML.load_file(".github/workflows/cd.yml"); puts :ok'`. **Commit.**

---

## Done criteria

- `dotnet build Fmis.slnx` clean; `dotnet test Fmis.slnx` green (backend + persistence + identity + application).
- ACR lives in `persistence`, deletion-protected, **admin disabled**; the identity + `AcrPull`/`AcrPush`/Postgres-grant live in `identity`; `application` references both and pushes to the pre-existing ACR (`az acr login` in CD, deploy principal `AcrPush`).
- CD deploys `auth → persistence → identity → application`.
- **Deferred (3c):** live login + Playwright; `CREATE EXTENSION postgis`.
```
