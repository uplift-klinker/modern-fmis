using System.Collections.Generic;
using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Persistence.Components;

public sealed class DatabaseIdentity : ComponentResource
{
    public Output<string> ClientId { get; }
    public Output<string> PrincipalId { get; }
    public Output<string> IdentityName { get; }

    public DatabaseIdentity(
        string name,
        Input<string> resourceGroupName,
        string location,
        Input<string> serverFqdn,
        Input<string> databaseName,
        string entraAdminLogin,
        Output<string> adminToken,
        InputList<Resource> dependsOn,
        ComponentResourceOptions? options = null)
        : base("fmis:persistence:DatabaseIdentity", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var identity = new AzureNative.ManagedIdentity.UserAssignedIdentity(name, new AzureNative.ManagedIdentity.UserAssignedIdentityArgs
        {
            ResourceGroupName = resourceGroupName,
            ResourceName = name,
            Location = location,
        }, childOptions);

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
            Create = Output.Format($"psql \"host={serverFqdn} port=5432 dbname={databaseName} user={entraAdminLogin} sslmode=require\" -v ON_ERROR_STOP=1 -c \"select * from pgaadauth_create_principal('{name}', false, false);\""),
            Environment = { ["PGPASSWORD"] = adminToken },
        }, new CustomResourceOptions { Parent = this, DependsOn = dependsOn });

        var grant = new Pulumi.PostgreSql.Grant($"{name}-grant", new Pulumi.PostgreSql.GrantArgs
        {
            Database = databaseName,
            Role = name,
            ObjectType = "database",
            Privileges = { "CONNECT", "CREATE", "TEMPORARY" },
        }, new CustomResourceOptions { Parent = this, Provider = provider, DependsOn = { principal } });

        ClientId = identity.ClientId;
        PrincipalId = identity.PrincipalId;
        IdentityName = Output.Create(name);
        RegisterOutputs();
    }
}
