using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Identity.Components;

public sealed class AppIdentity : ComponentResource
{
    public Output<string> ClientId { get; }
    public Output<string> PrincipalId { get; }
    public Output<string> Name { get; }
    public Output<string> ResourceId { get; }

    public AppIdentity(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:identity:AppIdentity", name, options)
    {
        var identity = new AzureNative.ManagedIdentity.UserAssignedIdentity(name, new AzureNative.ManagedIdentity.UserAssignedIdentityArgs
        {
            ResourceGroupName = resourceGroupName,
            ResourceName = name,
            Location = location,
        }, new CustomResourceOptions { Parent = this });

        ClientId = identity.ClientId;
        PrincipalId = identity.PrincipalId;
        Name = Output.Create(name);
        ResourceId = identity.Id;
        RegisterOutputs();
    }
}
