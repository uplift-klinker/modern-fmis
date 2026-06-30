using Pulumi;
using AzureNative = Pulumi.AzureNative;
using Fmis.Infra.Common;
using Fmis.Infra.Persistence.Components;

namespace Fmis.Infra.Persistence;

public class PersistenceStack : Stack
{
    [Output("serverFqdn")] public Output<string> ServerFqdn { get; private set; } = null!;
    [Output("databaseName")] public Output<string> DatabaseName { get; private set; } = null!;
    [Output("appIdentityClientId")] public Output<string> AppIdentityClientId { get; private set; } = null!;
    [Output("appIdentityPrincipalId")] public Output<string> AppIdentityPrincipalId { get; private set; } = null!;
    [Output("appIdentityName")] public Output<string> AppIdentityName { get; private set; } = null!;

    public PersistenceStack()
    {
        var env = Deployment.Instance.StackName;
        const string location = "centralus";
        const string entraAdminLogin = "fmis-ci-deployer";

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
            location,
            entraAdminLogin);

        var identity = new DatabaseIdentity(
            ResourceNames.For(env, "app", "identity"),
            resourceGroup.Name,
            location,
            server.Fqdn,
            server.DatabaseName,
            entraAdminLogin,
            PostgresAdminToken.Provider(),
            new InputList<Resource> { server.DeployerFirewallRule, server.EntraAdministrator });

        ServerFqdn = server.Fqdn;
        DatabaseName = server.DatabaseName;
        AppIdentityClientId = identity.ClientId;
        AppIdentityPrincipalId = identity.PrincipalId;
        AppIdentityName = identity.IdentityName;
    }
}
