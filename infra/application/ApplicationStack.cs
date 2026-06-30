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

        var auth = new StackReference("auth", new StackReferenceArgs { Name = $"fmis-auth/{env}" });
        var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"fmis-persistence/{env}" });

        var persistenceRg = $"fmis-{env}-persistence-rg";
        var clientConfig = AzureNative.Authorization.GetClientConfig.Invoke();

        var identityResourceId = Output.Tuple(
            clientConfig.Apply(c => c.SubscriptionId),
            persistence.GetOutput("appIdentityName").Apply(v => v!.ToString()!)
        ).Apply(t =>
            $"/subscriptions/{t.Item1}/resourceGroups/{persistenceRg}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{t.Item2}");

        var backend = new BackendApp(
            $"fmis-{env}-backend",
            resourceGroup.Name,
            location,
            imageRef: "mcr.microsoft.com/azuredocs/aci-helloworld:latest",
            acrLoginServer: registry.LoginServer,
            identityResourceId: identityResourceId,
            identityClientId: persistence.GetOutput("appIdentityClientId").Apply(v => v!.ToString()!),
            identityPrincipalId: persistence.GetOutput("appIdentityPrincipalId").Apply(v => v!.ToString()!),
            serverFqdn: persistence.GetOutput("serverFqdn").Apply(v => v!.ToString()!),
            databaseName: persistence.GetOutput("databaseName").Apply(v => v!.ToString()!),
            identityName: persistence.GetOutput("appIdentityName").Apply(v => v!.ToString()!),
            authDomain: auth.GetOutput("domain").Apply(v => v!.ToString()!),
            audience: auth.GetOutput("audience").Apply(v => v!.ToString()!),
            frontendUrl: Output.Create(""),
            registryId: registry.Registry.Id);
    }
}
