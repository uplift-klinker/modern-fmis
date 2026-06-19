using Pulumi;
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Auth.Components;

public sealed class TenantConfiguration : ComponentResource
{
    public TenantConfiguration(string name, ComponentResourceOptions? options = null)
        : base("fmis:auth:TenantConfiguration", name, options)
    {
        _ = new Auth0.Tenant(name, new Auth0.TenantArgs
        {
            DefaultDirectory = "Username-Password-Authentication",
        }, new CustomResourceOptions { Parent = this });

        RegisterOutputs();
    }
}
