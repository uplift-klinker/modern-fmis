using Pulumi;

namespace Fmis.Infra.Identity.Components;

public sealed class DatabaseAccess : ComponentResource
{
    public DatabaseAccess(
        string name,
        Input<string> serverFqdn,
        Input<string> databaseName,
        Input<string> identityName,
        string entraAdminLogin,
        Output<string> adminToken,
        ComponentResourceOptions? options = null)
        : base("fmis:identity:DatabaseAccess", name, options)
    {
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
            Create = Output.Format($"psql \"host={serverFqdn} port=5432 dbname={databaseName} user={entraAdminLogin} sslmode=require\" -v ON_ERROR_STOP=1 -c \"select * from pgaadauth_create_principal('{identityName}', false, false);\""),
            Environment = { ["PGPASSWORD"] = adminToken },
        }, new CustomResourceOptions { Parent = this });

        var grant = new Pulumi.PostgreSql.Grant($"{name}-grant", new Pulumi.PostgreSql.GrantArgs
        {
            Database = databaseName,
            Role = identityName,
            ObjectType = "database",
            Privileges = { "CONNECT", "CREATE", "TEMPORARY" },
        }, new CustomResourceOptions { Parent = this, Provider = provider, DependsOn = { principal } });

        RegisterOutputs();
    }
}
