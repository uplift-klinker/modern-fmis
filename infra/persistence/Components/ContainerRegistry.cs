using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Persistence.Components;

public sealed class ContainerRegistry : ComponentResource
{
    public Output<string> LoginServer { get; }
    public Output<string> RegistryId { get; }
    public AzureNative.ContainerRegistry.Registry Registry { get; }

    public ContainerRegistry(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:persistence:ContainerRegistry", name, options)
    {
        Registry = new AzureNative.ContainerRegistry.Registry(name, new AzureNative.ContainerRegistry.RegistryArgs
        {
            ResourceGroupName = resourceGroupName,
            RegistryName = name,
            Location = location,
            Sku = new AzureNative.ContainerRegistry.Inputs.SkuArgs { Name = AzureNative.ContainerRegistry.SkuName.Basic },
            AdminUserEnabled = false,
        }, new CustomResourceOptions { Parent = this, Protect = true });

        _ = new AzureNative.Authorization.ManagementLockByScope($"{name}-lock", new AzureNative.Authorization.ManagementLockByScopeArgs
        {
            Scope = Registry.Id,
            LockName = $"{name}-acr-cannotdelete",
            Level = AzureNative.Authorization.LockLevel.CanNotDelete,
        }, new CustomResourceOptions { Parent = this });

        LoginServer = Registry.LoginServer;
        RegistryId = Registry.Id;
        RegisterOutputs();
    }
}
