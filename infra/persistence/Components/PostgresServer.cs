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
                ActiveDirectoryAuth = AzureNative.DBforPostgreSQL.MicrosoftEntraAuth.Enabled,
                PasswordAuth = AzureNative.DBforPostgreSQL.PasswordBasedAuth.Disabled,
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
