using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Persistence.Components;

public sealed class PostgresServer : ComponentResource
{
    public Output<string> Fqdn { get; }
    public Output<string> DatabaseName { get; }
    public Resource DeployerFirewallRule { get; }
    public Resource EntraAdministrator { get; }

    public PostgresServer(string name, Input<string> resourceGroupName, string location, string entraAdminLogin, ComponentResourceOptions? options = null)
        : base("fmis:persistence:PostgresServer", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };
        var serverOptions = new CustomResourceOptions { Parent = this, Protect = true };

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
                ActiveDirectoryAuth = AzureNative.DBforPostgreSQL.MicrosoftEntraAuth.Enabled,
                PasswordAuth = AzureNative.DBforPostgreSQL.PasswordBasedAuth.Disabled,
            },
            Network = new AzureNative.DBforPostgreSQL.Inputs.NetworkArgs
            {
                PublicNetworkAccess = AzureNative.DBforPostgreSQL.ServerPublicNetworkAccessState.Enabled,
            },
            CreateMode = AzureNative.DBforPostgreSQL.CreateMode.Default,
        }, serverOptions);

        _ = new AzureNative.DBforPostgreSQL.FirewallRule($"{name}-allow-azure", new AzureNative.DBforPostgreSQL.FirewallRuleArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            FirewallRuleName = "AllowAllAzureServices",
            StartIpAddress = "0.0.0.0",
            EndIpAddress = "0.0.0.0",
        }, childOptions);

        var deployerIp = Environment.GetEnvironmentVariable("DEPLOYER_IP")
            ?? throw new InvalidOperationException("DEPLOYER_IP environment variable is required.");
        DeployerFirewallRule = new AzureNative.DBforPostgreSQL.FirewallRule($"{name}-allow-deployer", new AzureNative.DBforPostgreSQL.FirewallRuleArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            FirewallRuleName = "AllowDeployer",
            StartIpAddress = deployerIp,
            EndIpAddress = deployerIp,
        }, childOptions);

        _ = new AzureNative.DBforPostgreSQL.Configuration($"{name}-azure-extensions", new AzureNative.DBforPostgreSQL.ConfigurationArgs
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

        var adminObjectId = Environment.GetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID")
            ?? throw new InvalidOperationException("DEPLOY_PRINCIPAL_OBJECT_ID environment variable is required.");
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
            ?? throw new InvalidOperationException("AZURE_TENANT_ID environment variable is required.");

        EntraAdministrator = new AzureNative.DBforPostgreSQL.Administrator($"{name}-entra-admin", new AzureNative.DBforPostgreSQL.AdministratorArgs
        {
            ResourceGroupName = resourceGroupName,
            ServerName = server.Name,
            ObjectId = adminObjectId,
            PrincipalName = entraAdminLogin,
            PrincipalType = AzureNative.DBforPostgreSQL.PrincipalType.ServicePrincipal,
            TenantId = tenantId,
        }, childOptions);

        _ = new AzureNative.Authorization.ManagementLockByScope($"{name}-lock", new AzureNative.Authorization.ManagementLockByScopeArgs
        {
            Scope = server.Id,
            LockName = $"{name}-cannotdelete",
            Level = AzureNative.Authorization.LockLevel.CanNotDelete,
        }, childOptions);

        Fqdn = server.FullyQualifiedDomainName;
        DatabaseName = database.Name;
        RegisterOutputs();
    }
}
