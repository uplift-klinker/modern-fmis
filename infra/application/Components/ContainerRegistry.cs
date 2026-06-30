using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Application.Components;

public sealed class ContainerRegistry : ComponentResource
{
    public Output<string> LoginServer { get; }
    public Output<string> Name { get; }
    public AzureNative.ContainerRegistry.Registry Registry { get; }

    public ContainerRegistry(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:application:ContainerRegistry", name, options)
    {
        Registry = new AzureNative.ContainerRegistry.Registry(name, new AzureNative.ContainerRegistry.RegistryArgs
        {
            ResourceGroupName = resourceGroupName,
            RegistryName = name,
            Location = location,
            Sku = new AzureNative.ContainerRegistry.Inputs.SkuArgs { Name = AzureNative.ContainerRegistry.SkuName.Basic },
            AdminUserEnabled = true,
        }, new CustomResourceOptions { Parent = this });

        LoginServer = Registry.LoginServer;
        Name = Registry.Name;
        RegisterOutputs();
    }
}
