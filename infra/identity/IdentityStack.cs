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
    }
}
