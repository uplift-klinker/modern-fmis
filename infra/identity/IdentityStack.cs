using Pulumi;
using AzureNative = Pulumi.AzureNative;
using Fmis.Infra.Common;
using Fmis.Infra.Identity.Components;

namespace Fmis.Infra.Identity;

public class IdentityStack : Stack
{
    [Output("appIdentityClientId")] public Output<string> AppIdentityClientId { get; private set; }
    [Output("appIdentityPrincipalId")] public Output<string> AppIdentityPrincipalId { get; private set; }
    [Output("appIdentityName")] public Output<string> AppIdentityName { get; private set; }
    [Output("appIdentityResourceId")] public Output<string> AppIdentityResourceId { get; private set; }

    public IdentityStack()
    {
        var env = Deployment.Instance.StackName;
        const string location = "centralus";

        var resourceGroup = new AzureNative.Resources.ResourceGroup(
            ResourceNames.For(env, "identity", "rg"),
            new AzureNative.Resources.ResourceGroupArgs
            {
                ResourceGroupName = ResourceNames.For(env, "identity", "rg"),
                Location = location,
            });

        var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"organization/fmis-persistence/{env}" });

        var appIdentity = new AppIdentity(ResourceNames.For(env, "app", "identity"), resourceGroup.Name, location);

        var deployerPrincipalId = Environment.GetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID")
            ?? throw new InvalidOperationException("DEPLOY_PRINCIPAL_OBJECT_ID environment variable is required.");
        var acrId = persistence.RequireString("acrId");
        var registryAccess = new RegistryAccess(ResourceNames.For(env, "identity", "registry-access"), acrId, appIdentity.PrincipalId, deployerPrincipalId);

        var databaseAccess = new DatabaseAccess(
            ResourceNames.For(env, "identity", "db-access"),
            persistence.RequireString("serverFqdn"),
            persistence.RequireString("databaseName"),
            appIdentity.Name,
            "fmis-ci-deployer",
            PostgresAdminToken.Provider());

        AppIdentityClientId = appIdentity.ClientId;
        AppIdentityPrincipalId = appIdentity.PrincipalId;
        AppIdentityName = appIdentity.Name;
        AppIdentityResourceId = appIdentity.ResourceId;
    }
}
