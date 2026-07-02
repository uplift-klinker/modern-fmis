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

        var auth = new StackReference("auth", new StackReferenceArgs { Name = $"fmis-auth/{env}" });
        var persistence = new StackReference("persistence", new StackReferenceArgs { Name = $"fmis-persistence/{env}" });
        var identity = new StackReference("identity", new StackReferenceArgs { Name = $"fmis-identity/{env}" });

        var acrLoginServer = persistence.GetOutput("acrLoginServer").Apply(v => v!.ToString()!);

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
            identityResourceId: identity.GetOutput("appIdentityResourceId").Apply(v => v!.ToString()!),
            identityClientId: identity.GetOutput("appIdentityClientId").Apply(v => v!.ToString()!),
            identityName: identity.GetOutput("appIdentityName").Apply(v => v!.ToString()!),
            serverFqdn: persistence.GetOutput("serverFqdn").Apply(v => v!.ToString()!),
            databaseName: persistence.GetOutput("databaseName").Apply(v => v!.ToString()!),
            authDomain: auth.GetOutput("domain").Apply(v => v!.ToString()!),
            audience: auth.GetOutput("audience").Apply(v => v!.ToString()!),
            frontendUrl: frontendSite.Url,
            options: new ComponentResourceOptions { DependsOn = { image } });

        frontendSite.WriteConfig(
            backendUrl: backend.Url,
            authDomain: auth.GetOutput("domain").Apply(v => v!.ToString()!),
            spaClientId: auth.GetOutput("spaClientId").Apply(v => v!.ToString()!),
            audience: auth.GetOutput("audience").Apply(v => v!.ToString()!));

        BackendUrl = backend.Url;
        FrontendUrl = frontendSite.Url;
    }
}
