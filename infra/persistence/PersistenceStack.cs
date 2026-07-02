using Pulumi;
using AzureNative = Pulumi.AzureNative;
using Fmis.Infra.Common;
using Fmis.Infra.Persistence.Components;

namespace Fmis.Infra.Persistence;

public class PersistenceStack : Stack
{
    [Output("serverFqdn")] public Output<string> ServerFqdn { get; private set; } = null!;
    [Output("databaseName")] public Output<string> DatabaseName { get; private set; } = null!;
    [Output("acrLoginServer")] public Output<string> AcrLoginServer { get; private set; } = null!;
    [Output("acrId")] public Output<string> AcrId { get; private set; } = null!;
    [Output("acrName")] public Output<string> AcrName { get; private set; } = null!;

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

        var registry = new ContainerRegistry($"fmis{env}acr", resourceGroup.Name, location);

        ServerFqdn = server.Fqdn;
        DatabaseName = server.DatabaseName;
        AcrLoginServer = registry.LoginServer;
        AcrId = registry.RegistryId;
        AcrName = Output.Create($"fmis{env}acr");
    }
}
