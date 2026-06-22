using Pulumi;
using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Persistence.Components;

public sealed class DatabaseIdentity : ComponentResource
{
    public Output<string> ClientId { get; }
    public Output<string> PrincipalId { get; }
    public Output<string> IdentityName { get; }

    public DatabaseIdentity(string name, Input<string> resourceGroupName, string location, ComponentResourceOptions? options = null)
        : base("fmis:persistence:DatabaseIdentity", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var identity = new AzureNative.ManagedIdentity.UserAssignedIdentity(name, new AzureNative.ManagedIdentity.UserAssignedIdentityArgs
        {
            ResourceGroupName = resourceGroupName,
            ResourceName = name,
            Location = location,
        }, childOptions);

        ClientId = identity.ClientId;
        PrincipalId = identity.PrincipalId;
        IdentityName = Output.Create(name);
        RegisterOutputs();
    }
}
