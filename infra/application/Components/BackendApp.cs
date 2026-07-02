using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Application.Components;

public sealed class BackendApp : ComponentResource
{
    public Output<string> Url { get; }

    public BackendApp(
        string name,
        Input<string> resourceGroupName,
        string location,
        Input<string> imageRef,
        Input<string> acrLoginServer,
        Input<string> identityResourceId,
        Input<string> identityClientId,
        Input<string> serverFqdn,
        Input<string> databaseName,
        Input<string> identityName,
        Input<string> authDomain,
        Input<string> audience,
        Input<string> frontendUrl,
        ComponentResourceOptions? options = null)
        : base("fmis:application:BackendApp", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var managedEnvironment = new AzureNative.App.ManagedEnvironment($"{name}-env", new AzureNative.App.ManagedEnvironmentArgs
        {
            ResourceGroupName = resourceGroupName,
            Location = location,
        }, childOptions);

        var connectionString = Output.Format(
            $"Host={serverFqdn};Database={databaseName};Username={identityName};Ssl Mode=Require");
        var authority = Output.Format($"https://{authDomain}/");

        var app = new AzureNative.App.ContainerApp(name, new AzureNative.App.ContainerAppArgs
        {
            ResourceGroupName = resourceGroupName,
            Location = location,
            ManagedEnvironmentId = managedEnvironment.Id,
            Identity = new AzureNative.App.Inputs.ManagedServiceIdentityArgs
            {
                Type = AzureNative.App.ManagedServiceIdentityType.UserAssigned,
                UserAssignedIdentities = new InputList<string> { identityResourceId },
            },
            Configuration = new AzureNative.App.Inputs.ConfigurationArgs
            {
                Ingress = new AzureNative.App.Inputs.IngressArgs
                {
                    External = true,
                    TargetPort = 8080,
                },
                Registries = new[]
                {
                    new AzureNative.App.Inputs.RegistryCredentialsArgs
                    {
                        Server = acrLoginServer,
                        Identity = identityResourceId,
                    }
                },
            },
            Template = new AzureNative.App.Inputs.TemplateArgs
            {
                Containers = new[]
                {
                    new AzureNative.App.Inputs.ContainerArgs
                    {
                        Name = "backend",
                        Image = imageRef,
                        Env = new InputList<AzureNative.App.Inputs.EnvironmentVarArgs>
                        {
                            new AzureNative.App.Inputs.EnvironmentVarArgs { Name = "ConnectionStrings__Fmis", Value = connectionString },
                            new AzureNative.App.Inputs.EnvironmentVarArgs { Name = "Database__UseEntraAuth", Value = "true" },
                            new AzureNative.App.Inputs.EnvironmentVarArgs { Name = "AZURE_CLIENT_ID", Value = identityClientId },
                            new AzureNative.App.Inputs.EnvironmentVarArgs { Name = "Auth0__Authority", Value = authority },
                            new AzureNative.App.Inputs.EnvironmentVarArgs { Name = "Auth0__Audience", Value = audience },
                            new AzureNative.App.Inputs.EnvironmentVarArgs { Name = "Cors__AllowedOrigin", Value = frontendUrl },
                        },
                    }
                },
                Scale = new AzureNative.App.Inputs.ScaleArgs
                {
                    MinReplicas = 0,
                    MaxReplicas = 2,
                },
            },
        }, childOptions);

        Url = app.Configuration.Apply(c => $"https://{c!.Ingress!.Fqdn}");
        RegisterOutputs();
    }
}
