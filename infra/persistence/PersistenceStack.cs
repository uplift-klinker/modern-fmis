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

        var identity = new DatabaseIdentity(
            ResourceNames.For(env, "app", "identity"),
            resourceGroup.Name,
            location,
            server.Fqdn,
            server.DatabaseName,
            "fmis-ci-deployer",
            PostgresAdminToken.Provider(),
            new InputList<Resource> { server.DeployerFirewallRule, server.EntraAdministrator });
    }
}
