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

        var imageTag = Output.Format($"{registry.LoginServer}/fmis-backend:latest");

        var image = new Pulumi.DockerBuild.Image($"fmis{env}acr-backend", new Pulumi.DockerBuild.ImageArgs
        {
            Context = new Pulumi.DockerBuild.Inputs.BuildContextArgs { Location = "../.." },
            Dockerfile = new Pulumi.DockerBuild.Inputs.DockerfileArgs { Location = "../../backend/src/Fmis.Api/Dockerfile" },
            Tags = new InputList<string> { imageTag },
            Push = true,
            Registries = new[]
            {
                new Pulumi.DockerBuild.Inputs.RegistryArgs
                {
                    Address = registry.LoginServer,
                }
            },
        }, new CustomResourceOptions { DependsOn = { registry.Registry } });

        var auth = new StackReference("auth", new StackReferenceArgs { Name = $"fmis-auth/{env}" });
        var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"fmis-persistence/{env}" });

        var frontendSite = new FrontendSite($"fmis{env}web", resourceGroup.Name, location);

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
            imageRef: imageTag,
            acrLoginServer: registry.LoginServer,
            identityResourceId: identityResourceId,
            identityClientId: persistence.GetOutput("appIdentityClientId").Apply(v => v!.ToString()!),
            identityPrincipalId: persistence.GetOutput("appIdentityPrincipalId").Apply(v => v!.ToString()!),
            serverFqdn: persistence.GetOutput("serverFqdn").Apply(v => v!.ToString()!),
            databaseName: persistence.GetOutput("databaseName").Apply(v => v!.ToString()!),
            identityName: persistence.GetOutput("appIdentityName").Apply(v => v!.ToString()!),
            authDomain: auth.GetOutput("domain").Apply(v => v!.ToString()!),
            audience: auth.GetOutput("audience").Apply(v => v!.ToString()!),
            frontendUrl: frontendSite.Url,
            registryId: registry.Registry.Id,
            options: new ComponentResourceOptions { DependsOn = { image } });

        frontendSite.WriteConfig(
            backendUrl: backend.Url,
            authDomain: auth.GetOutput("domain").Apply(v => v!.ToString()!),
            spaClientId: auth.GetOutput("spaClientId").Apply(v => v!.ToString()!),
            audience: auth.GetOutput("audience").Apply(v => v!.ToString()!));
    }
}
