# Persistence Stack (Phase 3b‑1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provision the Postgres persistence tier (Entra-only auth, deletion-protected) and a user-assigned managed identity authorized to it entirely through Pulumi, emitting the outputs Phase 3b‑2 consumes — no passwords.

**Architecture:** A new Pulumi C# project `infra/persistence` (project `fmis-persistence`, `dev` stack) added to the root `Fmis.slnx`, mirroring `infra/auth`. A thin `PersistenceStack` composes two sealed `ComponentResource`s — `PostgresServer` (Flexible Server + firewall + database + PostGIS allowlist + deletion protection + Entra admin) and `DatabaseIdentity` (user-assigned MI + its Entra-mapped Postgres principal + privilege grants). Authorization runs in `pulumi up` via the `pulumi-postgresql` provider (grants) and a `pulumi-command` resource (the Azure `pgaadauth_create_principal`), authenticated with an Entra token.

**Tech Stack:** .NET 10, Pulumi C# + `Pulumi.AzureNative` + `Pulumi.PostgreSql` + `Pulumi.Command` + `Azure.Identity`, `Pulumi.Testing` + xUnit.

---

## Provider/SDK API note (read first)

`Pulumi.AzureNative`, `Pulumi.PostgreSql`, and `Pulumi.Command` resource and property names are **version-sensitive**. The code in this plan targets the current SDKs but the implementer MUST verify exact names against the pinned package versions and adjust when a build error reveals a renamed/moved member (e.g. the PostgreSQL Flexible Server lives under `Pulumi.AzureNative.DBforPostgreSQL`; arg shapes for `Sku`/`Storage`/`AuthConfig`/`Backup`/`Network` evolve between AzureNative versions). That is expected for IaC, not a deviation. Pin every package to its resolved version (repo convention: exact versions). Run `dotnet` from the **repo root** (single `Fmis.slnx`). All shell via `zsh -lc`.

The load-bearing contract is the **test assertions** (the resource graph each task asserts). When an Azure property name differs, keep the assertion's intent and adjust the property.

---

## File structure

```
infra/persistence/
  Fmis.Infra.Persistence.csproj   # Pulumi program; refs Common + Pulumi + AzureNative + PostgreSql + Command + Azure.Identity
  Pulumi.yaml                     # name: fmis-persistence, runtime: dotnet
  Pulumi.dev.yaml                 # dev stack config (non-secret)
  Program.cs                      # Deployment.RunAsync<PersistenceStack>
  PersistenceStack.cs             # thin composition root: RG + PostgresServer + DatabaseIdentity + outputs
  PostgresAdminToken.cs           # injectable Entra-token seam (default Azure.Identity; tests override)
  Components/
    PostgresServer.cs             # Flexible Server + firewall + db + PostGIS allowlist + protection + Entra admin
    DatabaseIdentity.cs           # user-assigned MI + pgaadauth principal (command) + grants (postgresql provider)
infra/tests/Fmis.Infra.Tests/
  InfraTesting.cs                 # ADD RunPersistenceStackAsync (env + token-seam override, restored)
  PersistenceStackTests.cs        # Pulumi.Testing assertions for the persistence resource graph
.github/workflows/infra.yml       # extend: deploy the persistence stack (preview/up) + DEPLOYER_IP/DEPLOY_PRINCIPAL_OBJECT_ID
Fmis.slnx                         # add the persistence project
```

Naming via `Fmis.Infra.Common.ResourceNames.For(env, "persistence", …)`. The database is named exactly `fmis`; the identity `fmis-dev-app-identity`. Default region `centralus`.

---

## Task 1: Scaffold the persistence project

**Files:** Create `infra/persistence/Fmis.Infra.Persistence.csproj`, `infra/persistence/Pulumi.yaml`, `infra/persistence/PersistenceStack.cs`, `infra/persistence/Program.cs`; Modify `Fmis.slnx`, `infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj`.

- [ ] **Step 1: Create the project + packages**

```bash
cd /Users/bryceklinker/code/uplift-delivery/modern-fmis/infra
mkdir -p persistence
zsh -lc 'cd infra/persistence && dotnet new console -n Fmis.Infra.Persistence -o . && rm -f Program.cs'
zsh -lc 'cd infra/persistence && dotnet add Fmis.Infra.Persistence.csproj package Pulumi'
zsh -lc 'cd infra/persistence && dotnet add Fmis.Infra.Persistence.csproj package Pulumi.AzureNative'
zsh -lc 'cd infra/persistence && dotnet add Fmis.Infra.Persistence.csproj package Pulumi.PostgreSql'
zsh -lc 'cd infra/persistence && dotnet add Fmis.Infra.Persistence.csproj package Pulumi.Command'
zsh -lc 'cd infra/persistence && dotnet add Fmis.Infra.Persistence.csproj package Azure.Identity'
zsh -lc 'cd infra/persistence && dotnet add Fmis.Infra.Persistence.csproj reference ../Fmis.Infra.Common/Fmis.Infra.Common.csproj'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet sln Fmis.slnx add infra/persistence/Fmis.Infra.Persistence.csproj'
zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet add infra/tests/Fmis.Infra.Tests/Fmis.Infra.Tests.csproj reference infra/persistence/Fmis.Infra.Persistence.csproj'
```
Pin every added package to its resolved version in the csproj (trim duplicated `TargetFramework`/`Nullable`/`ImplicitUsings` — they come from the root `Directory.Build.props`). Add the project to the `/infra/` solution folder in `Fmis.slnx`.

`infra/persistence/Pulumi.yaml`:
```yaml
name: fmis-persistence
runtime: dotnet
description: PostgreSQL persistence tier + the app's database managed identity
```

- [ ] **Step 2: Empty stack + program**

`infra/persistence/PersistenceStack.cs`:
```csharp
using Pulumi;

namespace Fmis.Infra.Persistence;

public class PersistenceStack : Stack
{
    public PersistenceStack()
    {
    }
}
```

`infra/persistence/Program.cs`:
```csharp
using Pulumi;

return await Deployment.RunAsync<Fmis.Infra.Persistence.PersistenceStack>();
```

- [ ] **Step 3: Build**

Run: `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet build Fmis.slnx'`
Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add infra Fmis.slnx && git commit -m "Scaffold infra/persistence Pulumi project

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Test harness for the persistence stack + the injectable token seam

**Files:** Create `infra/persistence/PostgresAdminToken.cs`; Modify `infra/tests/Fmis.Infra.Tests/InfraTesting.cs`.

- [ ] **Step 1: Add the token seam**

`infra/persistence/PostgresAdminToken.cs` — the Entra admin token used to configure the postgresql provider/command. Default fetches via `Azure.Identity`; tests override it (so unit tests never hit Azure):
```csharp
using Azure.Core;
using Azure.Identity;
using Pulumi;

namespace Fmis.Infra.Persistence;

public static class PostgresAdminToken
{
    public static Func<Output<string>> Provider { get; set; } = FromAzureIdentity;

    private static Output<string> FromAzureIdentity() =>
        Output.CreateSecret(Output.Create(FetchAsync()));

    private static async Task<string> FetchAsync()
    {
        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" }));
        return token.Token;
    }
}
```

- [ ] **Step 2: Add the persistence test runner to `InfraTesting`**

In `infra/tests/Fmis.Infra.Tests/InfraTesting.cs`, add a runner that sets the env the persistence stack reads, overrides the token seam with a fake, and restores both in a finally (the existing `StackMocks`/`GetAsync` are reused):
```csharp
    public static async Task<ImmutableArray<Resource>> RunPersistenceStackAsync()
    {
        var previousDeployerIp = Environment.GetEnvironmentVariable("DEPLOYER_IP");
        var previousAdminObjectId = Environment.GetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID");
        var previousTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var previousTokenProvider = Fmis.Infra.Persistence.PostgresAdminToken.Provider;

        Environment.SetEnvironmentVariable("DEPLOYER_IP", "203.0.113.10");
        Environment.SetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID", "00000000-0000-0000-0000-000000000001");
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", "00000000-0000-0000-0000-0000000000aa");
        Fmis.Infra.Persistence.PostgresAdminToken.Provider = () => Output.CreateSecret("test-token");
        try
        {
            return await Deployment.TestAsync<Fmis.Infra.Persistence.PersistenceStack>(
                new StackMocks(),
                new TestOptions { StackName = "dev", ProjectName = "fmis-persistence", IsPreview = false });
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEPLOYER_IP", previousDeployerIp);
            Environment.SetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID", previousAdminObjectId);
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", previousTenantId);
            Fmis.Infra.Persistence.PostgresAdminToken.Provider = previousTokenProvider;
        }
    }
```

- [ ] **Step 3: Build**

Run: `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet build Fmis.slnx'` → succeeds.

- [ ] **Step 4: Commit**

```bash
git add infra && git commit -m "Add persistence token seam + Pulumi.Testing runner

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: PostgresServer — Flexible Server (Burstable B1ms, PG16, Entra-only)

**Files:** Create `infra/persistence/Components/PostgresServer.cs`, `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`; Modify `infra/persistence/PersistenceStack.cs`.

- [ ] **Step 1: Failing test**

`infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`:
```csharp
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Tests;

public class PersistenceStackTests
{
    [Fact]
    public async Task Creates_a_burstable_pg16_entra_only_server()
    {
        var resources = await InfraTesting.RunPersistenceStackAsync();

        var server = resources.OfType<AzureNative.DBforPostgreSQL.Server>().Single();
        Assert.Equal("fmis-dev-persistence-postgres", await InfraTesting.GetAsync(server.Name));
        Assert.Equal("16", await InfraTesting.GetAsync(server.Version));
    }
}
```
Verify the resource type/namespace (`AzureNative.DBforPostgreSQL.Server`) + `Name`/`Version` output members against the installed AzureNative version; adjust the `using`/members if needed.

- [ ] **Step 2: Run red**

Run: `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter "FullyQualifiedName~PersistenceStackTests"'` → FAIL (no server).

- [ ] **Step 3: Implement the server component + resource group, compose in the stack**

`infra/persistence/Components/PostgresServer.cs`:
```csharp
using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Persistence.Components;

public sealed class PostgresServer : ComponentResource
{
    public Output<string> Fqdn { get; }
    public Output<string> DatabaseName { get; }

    public PostgresServer(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:persistence:PostgresServer", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var server = new AzureNative.DBforPostgreSQL.Server(name, new AzureNative.DBforPostgreSQL.ServerArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = name,
            Location = location,
            Version = "16",
            Sku = new AzureNative.DBforPostgreSQL.Inputs.SkuArgs
            {
                Name = "Standard_B1ms",
                Tier = AzureNative.DBforPostgreSQL.SkuTier.Burstable,
            },
            Storage = new AzureNative.DBforPostgreSQL.Inputs.StorageArgs
            {
                StorageSizeGB = 32,
                AutoGrow = AzureNative.DBforPostgreSQL.StorageAutoGrow.Enabled,
            },
            Backup = new AzureNative.DBforPostgreSQL.Inputs.BackupArgs { BackupRetentionDays = 7 },
            AuthConfig = new AzureNative.DBforPostgreSQL.Inputs.AuthConfigArgs
            {
                ActiveDirectoryAuth = AzureNative.DBforPostgreSQL.ActiveDirectoryAuthEnum.Enabled,
                PasswordAuth = AzureNative.DBforPostgreSQL.PasswordAuthEnum.Disabled,
            },
            Network = new AzureNative.DBforPostgreSQL.Inputs.NetworkArgs
            {
                PublicNetworkAccess = AzureNative.DBforPostgreSQL.ServerPublicNetworkAccessState.Enabled,
            },
            CreateMode = AzureNative.DBforPostgreSQL.CreateMode.Default,
        }, childOptions);

        Fqdn = server.FullyQualifiedDomainName;
        DatabaseName = Output.Create("fmis");
        RegisterOutputs();
    }
}
```

In `PersistenceStack.cs`, create the resource group and the server:
```csharp
using Pulumi;
using AzureNative = Pulumi.AzureNative;
using Fmis.Infra.Common;
using Fmis.Infra.Persistence.Components;

namespace Fmis.Infra.Persistence;

public class PersistenceStack : Stack
{
    public PersistenceStack()
    {
        var env = Deployment.Instance.StackName;
        const string location = "centralus";

        var resourceGroup = new AzureNative.Resources.ResourceGroup(
            ResourceNames.For(env, "persistence", "rg"),
            new AzureNative.Resources.ResourceGroupArgs
            {
                ResourceGroupName = ResourceNames.For(env, "persistence", "rg"),
                Location = location,
            });

        var server = new PostgresServer(
            ResourceNames.For(env, "persistence", "postgres"),
            resourceGroup.Name,
            location);
    }
}
```
Verify the AzureNative arg/enum names (`SkuTier.Burstable`, `StorageAutoGrow`, `ActiveDirectoryAuthEnum`, `PasswordAuthEnum`, `ServerPublicNetworkAccessState`, `FullyQualifiedDomainName`) against the installed version and adjust. `location` is hard-coded `centralus` per the convention default.

- [ ] **Step 4: Run green + commit**

Run: `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter "FullyQualifiedName~PersistenceStackTests"'` → PASS.
```bash
git add infra && git commit -m "Add PostgresServer: Burstable PG16 Flexible Server, Entra-only auth (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: PostgresServer — firewall rules, PostGIS allowlist, fmis database

**Files:** Modify `infra/persistence/Components/PostgresServer.cs`, `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`.

- [ ] **Step 1: Failing tests**

Add to `PersistenceStackTests`:
```csharp
[Fact]
public async Task Allows_azure_services_and_the_deployer_ip()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    var rules = resources.OfType<AzureNative.DBforPostgreSQL.FirewallRule>().ToList();
    Assert.Contains(rules, r => InfraTesting.GetAsync(r.StartIpAddress).Result == "0.0.0.0"
        && InfraTesting.GetAsync(r.EndIpAddress).Result == "0.0.0.0");
    Assert.Contains(rules, r => InfraTesting.GetAsync(r.StartIpAddress).Result == "203.0.113.10");
}

[Fact]
public async Task Creates_the_fmis_database()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    var database = resources.OfType<AzureNative.DBforPostgreSQL.Database>().Single();
    Assert.Equal("fmis", await InfraTesting.GetAsync(database.Name));
}

[Fact]
public async Task Allowlists_the_postgis_extension()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    var config = resources.OfType<AzureNative.DBforPostgreSQL.Configuration>()
        .Single(c => InfraTesting.GetAsync(c.Name).Result == "azure.extensions");
    Assert.Contains("POSTGIS", await InfraTesting.GetAsync(config.Value));
}
```

- [ ] **Step 2: Run red**

Run: `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx --filter "FullyQualifiedName~PersistenceStackTests"'` → FAIL (no firewall rules / db / config).

- [ ] **Step 3: Implement** — add to the `PostgresServer` constructor (after the server), reading the deployer IP from env:
```csharp
        var _allowAzure = new AzureNative.DBforPostgreSQL.FirewallRule($"{name}-allow-azure", new AzureNative.DBforPostgreSQL.FirewallRuleArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            FirewallRuleName = "AllowAllAzureServices",
            StartIpAddress = "0.0.0.0",
            EndIpAddress = "0.0.0.0",
        }, childOptions);

        var deployerIp = Environment.GetEnvironmentVariable("DEPLOYER_IP")
            ?? throw new InvalidOperationException("DEPLOYER_IP environment variable is required.");
        var _allowDeployer = new AzureNative.DBforPostgreSQL.FirewallRule($"{name}-allow-deployer", new AzureNative.DBforPostgreSQL.FirewallRuleArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            FirewallRuleName = "AllowDeployer",
            StartIpAddress = deployerIp,
            EndIpAddress = deployerIp,
        }, childOptions);

        var _postgis = new AzureNative.DBforPostgreSQL.Configuration($"{name}-azure-extensions", new AzureNative.DBforPostgreSQL.ConfigurationArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            ConfigurationName = "azure.extensions",
            Value = "POSTGIS",
            Source = "user-override",
        }, childOptions);

        var database = new AzureNative.DBforPostgreSQL.Database($"{name}-database", new AzureNative.DBforPostgreSQL.DatabaseArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            DatabaseName = "fmis",
        }, childOptions);
```
Add `using System;` is unnecessary (ImplicitUsings). Verify `FirewallRule`/`Configuration`/`Database` arg names against the installed version. The `0.0.0.0`–`0.0.0.0` rule is Azure's "allow all Azure services" convention.

- [ ] **Step 4: Run green + commit**

Run: the same filter → PASS.
```bash
git add infra && git commit -m "Add firewall rules, PostGIS allowlist, and the fmis database (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: PostgresServer — Entra admin + deletion protection

**Files:** Modify `infra/persistence/Components/PostgresServer.cs`, `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`.

- [ ] **Step 1: Failing tests**

Add to `PersistenceStackTests`:
```csharp
[Fact]
public async Task Sets_the_ci_principal_as_the_entra_admin()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    var admin = resources.OfType<AzureNative.DBforPostgreSQL.Administrator>().Single();
    Assert.Equal("00000000-0000-0000-0000-000000000001", await InfraTesting.GetAsync(admin.ObjectId));
}

[Fact]
public async Task Locks_the_server_against_deletion()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    var locks = resources.OfType<AzureNative.Authorization.ManagementLockByScope>().ToList();
    Assert.Contains(locks, l => InfraTesting.GetAsync(l.Level).Result == "CanNotDelete");
}
```

- [ ] **Step 2: Run red** → FAIL (no Administrator / lock).

- [ ] **Step 3: Implement** — add to `PostgresServer` (the admin object id + tenant from env):
```csharp
        var adminObjectId = Environment.GetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID")
            ?? throw new InvalidOperationException("DEPLOY_PRINCIPAL_OBJECT_ID environment variable is required.");
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
            ?? throw new InvalidOperationException("AZURE_TENANT_ID environment variable is required.");

        var _admin = new AzureNative.DBforPostgreSQL.Administrator($"{name}-entra-admin", new AzureNative.DBforPostgreSQL.AdministratorArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            ObjectId = adminObjectId,
            PrincipalName = "fmis-ci-deployer",
            PrincipalType = AzureNative.DBforPostgreSQL.PrincipalType.ServicePrincipal,
            TenantId = tenantId,
        }, childOptions);

        var _lock = new AzureNative.Authorization.ManagementLockByScope($"{name}-lock", new AzureNative.Authorization.ManagementLockByScopeArgs
        {
            Scope = server.Id,
            LockName = $"{name}-cannotdelete",
            Level = AzureNative.Authorization.LockLevel.CanNotDelete,
        }, new CustomResourceOptions { Parent = this });
```
Also set Pulumi `protect: true` on the server resource: change the server's `CustomResourceOptions` to include `Protect = true` (i.e. construct the server with `new CustomResourceOptions { Parent = this, Protect = true }` instead of `childOptions`). Verify `Administrator`/`ManagementLockByScope`/`LockLevel.CanNotDelete`/`PrincipalType.ServicePrincipal` against the installed version.

- [ ] **Step 4: Run green + commit**

```bash
git add infra && git commit -m "Add Entra admin + deletion protection (protect + CanNotDelete lock) (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: DatabaseIdentity — user-assigned managed identity

**Files:** Create `infra/persistence/Components/DatabaseIdentity.cs`; Modify `infra/persistence/PersistenceStack.cs`, `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`.

- [ ] **Step 1: Failing test**

Add to `PersistenceStackTests`:
```csharp
[Fact]
public async Task Creates_the_app_managed_identity()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    var identity = resources.OfType<AzureNative.ManagedIdentity.UserAssignedIdentity>().Single();
    Assert.Equal("fmis-dev-app-identity", await InfraTesting.GetAsync(identity.Name));
}
```

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** the component (identity only for now) + compose:

`infra/persistence/Components/DatabaseIdentity.cs`:
```csharp
using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Persistence.Components;

public sealed class DatabaseIdentity : ComponentResource
{
    public Output<string> ClientId { get; }
    public Output<string> PrincipalId { get; }
    public Output<string> IdentityName { get; }

    public DatabaseIdentity(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:persistence:DatabaseIdentity", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var identity = new AzureNative.ManagedIdentity.UserAssignedIdentity(name, new AzureNative.ManagedIdentity.UserAssignedIdentityArgs
        {
            ResourceGroupName = resourceGroupName,
            ResourceName = name,
            Location = location,
        }, childOptions);

        ClientId = identity.ClientId;
        PrincipalId = identity.PrincipalId;
        IdentityName = Output.Create(name);
        RegisterOutputs();
    }
}
```

In `PersistenceStack.cs`, add after the server:
```csharp
        var identity = new DatabaseIdentity(
            ResourceNames.For(env, "app", "identity"),
            resourceGroup.Name,
            location);
```
(`ResourceNames.For("dev","app","identity")` → `fmis-dev-app-identity`.) Verify `UserAssignedIdentity` arg/output members.

- [ ] **Step 4: Run green + commit**

```bash
git add infra && git commit -m "Add the app user-assigned managed identity (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: DatabaseIdentity — Entra principal (command) + privilege grants (postgresql provider)

**Files:** Modify `infra/persistence/Components/DatabaseIdentity.cs`, `infra/persistence/PersistenceStack.cs`, `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`.

- [ ] **Step 1: Failing tests**

Add to `PersistenceStackTests`:
```csharp
[Fact]
public async Task Provisions_the_entra_principal_and_grants()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();

    Assert.NotEmpty(resources.OfType<Pulumi.Command.Local.Command>());
    Assert.NotEmpty(resources.OfType<Pulumi.PostgreSql.Grant>());
}
```
Verify the `pulumi-command` local command type (`Pulumi.Command.Local.Command`) and `Pulumi.PostgreSql.Grant` against the installed versions.

- [ ] **Step 2: Run red** → FAIL.

- [ ] **Step 3: Implement** — the component now needs the server FQDN, database name, the Entra admin name, and the admin token (from `PostgresAdminToken.Provider`). Extend `DatabaseIdentity`'s constructor to accept them and create: a `pulumi-postgresql` provider (host = FQDN, username = the Entra admin login, password = the admin token, SSL required), a `Command` running `pgaadauth_create_principal` for the identity (ordered after a `dependsOn` the deployer firewall rule), and a `Grant` of privileges on `fmis` to the identity principal using that provider. Pass the FQDN, database name, the deployer-firewall dependency, and the token from the stack.

```csharp
// constructor params added: Input<string> serverFqdn, Input<string> databaseName,
//   string entraAdminLogin, Output<string> adminToken, InputList<Resource> dependsOn

        var provider = new Pulumi.PostgreSql.Provider($"{name}-pg", new Pulumi.PostgreSql.ProviderArgs
        {
            Host = serverFqdn,
            Port = 5432,
            Username = entraAdminLogin,
            Password = adminToken,
            Sslmode = "require",
            Superuser = false,
        }, new CustomResourceOptions { Parent = this });

        var principal = new Pulumi.Command.Local.Command($"{name}-principal", new Pulumi.Command.Local.CommandArgs
        {
            Create = Output.Format($"psql \"host={serverFqdn} port=5432 dbname={databaseName} user={entraAdminLogin} sslmode=require\" -v ON_ERROR_STOP=1 -c \"select * from pgaadauth_create_principal('{name}', false, false);\"",
            Environment = adminToken.Apply(t => new Dictionary<string, string> { ["PGPASSWORD"] = t }),
        }, new CustomResourceOptions { Parent = this, DependsOn = dependsOn });

        var grant = new Pulumi.PostgreSql.Grant($"{name}-grant", new Pulumi.PostgreSql.GrantArgs
        {
            Database = databaseName,
            Role = name,
            ObjectType = "database",
            Privileges = { "CONNECT", "CREATE", "TEMPORARY" },
        }, new CustomResourceOptions { Parent = this, Provider = provider, DependsOn = { principal } });
```
In `PersistenceStack.cs`, pass the needed values from the server + the token seam, and order the identity after the server's deployer firewall rule. Expose the deployer firewall rule (and the Entra admin login) from `PostgresServer` so the stack can wire `dependsOn`/login; or pass the whole `PostgresServer` instance to `DatabaseIdentity`. Use `PostgresAdminToken.Provider()` for `adminToken`. The Entra admin login for Postgres is the deploy principal's name/appId used when the Administrator was created (`fmis-ci-deployer` / the app id) — confirm what Azure expects as the Entra login username for token auth (commonly the principal's UPN/app-id); wire it consistently with the Administrator's `PrincipalName`.

> This is the most provider-sensitive task. Get the test green (a `Command` + a `Grant` are registered) and the wiring sound; the exact `psql`/grant SQL is verified at deploy, not in unit tests (mocks don't execute it). Adjust `Provider`/`Grant`/`Command` property names to the installed SDKs.

- [ ] **Step 4: Run green + commit**

```bash
git add infra && git commit -m "Authorize the managed identity in Pulumi: Entra principal + DB grants (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 8: Stack outputs + dev config

**Files:** Modify `infra/persistence/PersistenceStack.cs`; Create `infra/persistence/Pulumi.dev.yaml`; Modify `infra/tests/Fmis.Infra.Tests/PersistenceStackTests.cs`.

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public async Task Exposes_server_database_and_identity_outputs()
{
    var resources = await InfraTesting.RunPersistenceStackAsync();
    var stack = resources.OfType<Fmis.Infra.Persistence.PersistenceStack>().Single();

    Assert.Equal("fmis", await InfraTesting.GetAsync(stack.DatabaseName));
    Assert.Equal("fmis-dev-app-identity", await InfraTesting.GetAsync(stack.AppIdentityName));
    Assert.NotNull(await InfraTesting.GetAsync(stack.ServerFqdn));
}
```

- [ ] **Step 2: Run red** → FAIL (no outputs).

- [ ] **Step 3: Implement** — add `[Output]` properties to `PersistenceStack` and assign them from the components:
```csharp
    [Output("serverFqdn")] public Output<string> ServerFqdn { get; private set; }
    [Output("databaseName")] public Output<string> DatabaseName { get; private set; }
    [Output("appIdentityClientId")] public Output<string> AppIdentityClientId { get; private set; }
    [Output("appIdentityPrincipalId")] public Output<string> AppIdentityPrincipalId { get; private set; }
    [Output("appIdentityName")] public Output<string> AppIdentityName { get; private set; }
```
Assign: `ServerFqdn = server.Fqdn; DatabaseName = server.DatabaseName; AppIdentityClientId = identity.ClientId; AppIdentityPrincipalId = identity.PrincipalId; AppIdentityName = identity.IdentityName;`.

`infra/persistence/Pulumi.dev.yaml`:
```yaml
config: {}
```
(No committed config — `DEPLOYER_IP`/`DEPLOY_PRINCIPAL_OBJECT_ID`/`AZURE_TENANT_ID` come from env in CD, like the auth stack's provider creds.)

- [ ] **Step 4: Run green + commit**

Run: `zsh -lc 'cd /Users/bryceklinker/code/uplift-delivery/modern-fmis && dotnet test Fmis.slnx'` → all green (auth + persistence).
```bash
git add infra && git commit -m "Expose persistence stack outputs (fqdn/db/identity) + dev config (TDD)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 9: CD — deploy the persistence stack

**Files:** Modify `.github/workflows/infra.yml`.

- [ ] **Step 1: Extend the workflow**

After the existing `auth` preview/up steps, add persistence steps that compute the env the stack needs and run `pulumi preview`/`up` on the persistence stack. Add a step to export the runner IP + the deploy principal's object id, and run pulumi in `infra/persistence`:
```yaml
      - name: Compute persistence deploy inputs
        run: |
          echo "DEPLOYER_IP=$(curl -s ifconfig.me)" >> "$GITHUB_ENV"
          echo "DEPLOY_PRINCIPAL_OBJECT_ID=$(az ad sp show --id ${{ secrets.AZURE_CLIENT_ID }} --query id -o tsv)" >> "$GITHUB_ENV"

      - name: Select or init the persistence dev stack
        working-directory: infra/persistence
        run: pulumi stack select dev || pulumi stack init dev
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}

      - name: Persistence preview (pull request)
        if: github.event_name == 'pull_request'
        working-directory: infra/persistence
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
        run: pulumi preview

      - name: Persistence up (merge to main)
        if: github.event_name == 'push'
        working-directory: infra/persistence
        env:
          PULUMI_CONFIG_PASSPHRASE: ${{ secrets.PULUMI_CONFIG_PASSPHRASE }}
        run: pulumi up --yes
```
`DEPLOYER_IP`/`DEPLOY_PRINCIPAL_OBJECT_ID`/`AZURE_TENANT_ID` are read from the job env (`AZURE_TENANT_ID` is already provided to the job via the `azure/login` secrets — add it to the job `env` if not already exposed to the program: `AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}`). `Azure.Identity` uses the existing OIDC login for the Postgres token. Keep `pulumi/actions@v7` + `pulumi-version: 3.247.0`.

- [ ] **Step 2: Validate YAML**

Run: `zsh -lc 'ruby -ryaml -e "YAML.load_file(\".github/workflows/infra.yml\"); puts :ok"'` → `ok`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/infra.yml && git commit -m "CD: deploy the persistence stack (preview/up) with deployer + admin inputs

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Done criteria

- `dotnet build Fmis.slnx` clean; `dotnet test Fmis.slnx` green (auth + persistence Pulumi.Testing suites).
- The persistence stack models: a Burstable PG16 Flexible Server (Entra-only auth, CI principal Entra admin), `AllowAllAzureServices` + deployer-IP firewall rules, the `azure.extensions` POSTGIS allowlist, the `fmis` database, `protect` + `CanNotDelete` lock, a user-assigned managed identity, its Entra principal (command) + DB grants (postgresql provider), and the outputs (`serverFqdn`/`databaseName`/`appIdentity*`) — no password.
- Unit tests never perform real Azure auth (token seam overridden); the data-plane SQL is verified at deploy.
- CD deploys the persistence stack; the post-merge `pulumi up` requires the existing Auth0/Azure bootstrap.
- **Deferred (3b‑2):** application stack, `config.json`, backend Entra-token code, assigning the identity to compute, `CREATE EXTENSION postgis`.
```
