using Pulumi;
using AzureNative = Pulumi.AzureNative;
using Fmis.Infra.Common;
using Fmis.Infra.Identity.Components;

namespace Fmis.Infra.Identity;

public class IdentityStack : Stack
{
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

        var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"fmis-persistence/{env}" });

        var appIdentity = new AppIdentity(ResourceNames.For(env, "app", "identity"), resourceGroup.Name, location);

        var deployerPrincipalId = Environment.GetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID")
            ?? throw new InvalidOperationException("DEPLOY_PRINCIPAL_OBJECT_ID environment variable is required.");
        var acrId = persistence.GetOutput("acrId").Apply(v => v!.ToString()!);
        var registryAccess = new RegistryAccess(ResourceNames.For(env, "identity", "registry-access"), acrId, appIdentity.PrincipalId, deployerPrincipalId);
    }
}
