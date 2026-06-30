using Pulumi;
using AzureNative = Pulumi.AzureNative;
using Fmis.Infra.Common;
using Fmis.Infra.Application.Components;

namespace Fmis.Infra.Application;

public class ApplicationStack : Stack
{
    public ApplicationStack()
    {
        var env = Deployment.Instance.StackName;
        const string location = "centralus";

        var resourceGroup = new AzureNative.Resources.ResourceGroup(
            ResourceNames.For(env, "application", "rg"),
            new AzureNative.Resources.ResourceGroupArgs
            {
                ResourceGroupName = ResourceNames.For(env, "application", "rg"),
                Location = location,
            });

        var registry = new ContainerRegistry($"fmis{env}acr", resourceGroup.Name, location);
    }
}
