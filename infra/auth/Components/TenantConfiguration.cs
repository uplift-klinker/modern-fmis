using Pulumi;
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Auth.Components;

public sealed class TenantConfiguration : ComponentResource
{
    public TenantConfiguration(string name, ComponentResourceOptions? options = null)
        : base("fmis:auth:TenantConfiguration", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        _ = new Auth0.Tenant(name, new Auth0.TenantArgs
        {
            DefaultDirectory = "Username-Password-Authentication",
        }, childOptions);

        _ = new Auth0.AttackProtection($"{name}-attack-protection", new Auth0.AttackProtectionArgs
        {
            BruteForceProtection = new Auth0.Inputs.AttackProtectionBruteForceProtectionArgs
            {
                Enabled = false,
            },
            SuspiciousIpThrottling = new Auth0.Inputs.AttackProtectionSuspiciousIpThrottlingArgs
            {
                Enabled = false,
            },
        }, childOptions);

        RegisterOutputs();
    }
}
