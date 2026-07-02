using Pulumi;
using AzureNative = Pulumi.AzureNative;
using Fmis.Infra.Common;
using Fmis.Infra.Application.Components;

namespace Fmis.Infra.Application;

public class ApplicationStack : Stack
{
    [Output("backendUrl")] public Output<string> BackendUrl { get; private set; }
    [Output("frontendUrl")] public Output<string> FrontendUrl { get; private set; }

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

        var auth = new StackReference("auth", new StackReferenceArgs { Name = $"organization/fmis-auth/{env}" });
        var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"organization/fmis-persistence/{env}" });
        var identity = new StackReference("identity", new StackReferenceArgs { Name = $"organization/fmis-identity/{env}" });

        var acrLoginServer = persistence.RequireString("acrLoginServer", $"fmis{env}acr.azurecr.io");

        var imageTag = Output.Format($"{acrLoginServer}/fmis-backend:latest");

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
                    Address = acrLoginServer,
                }
            },
        });

        var frontendSite = new FrontendSite($"fmis{env}web", resourceGroup.Name, location);

        var backend = new BackendApp(
            $"fmis-{env}-backend",
            resourceGroup.Name,
            location,
            imageRef: imageTag,
            acrLoginServer: acrLoginServer,
            identityResourceId: identity.RequireString("appIdentityResourceId",
                $"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/fmis-{env}-identity-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/fmis-{env}-app-identity"),
            identityClientId: identity.RequireString("appIdentityClientId"),
            identityName: identity.RequireString("appIdentityName"),
            serverFqdn: persistence.RequireString("serverFqdn"),
            databaseName: persistence.RequireString("databaseName"),
            authDomain: auth.RequireString("domain"),
            audience: auth.RequireString("audience"),
            frontendUrl: frontendSite.Url,
            options: new ComponentResourceOptions { DependsOn = { image } });

        frontendSite.WriteConfig(
            backendUrl: backend.Url,
            authDomain: auth.RequireString("domain"),
            spaClientId: auth.RequireString("spaClientId"),
            audience: auth.RequireString("audience"));

        BackendUrl = backend.Url;
        FrontendUrl = frontendSite.Url;
    }
}
