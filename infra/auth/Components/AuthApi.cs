using Pulumi;
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Auth.Components;

public sealed class AuthApi : ComponentResource
{
    public Output<string> Audience { get; }

    public AuthApi(string name, string identifier, ComponentResourceOptions? options = null)
        : base("fmis:auth:AuthApi", name, options)
    {
        var api = new Auth0.ResourceServer(name, new Auth0.ResourceServerArgs
        {
            Name = name,
            Identifier = identifier,
            SigningAlg = "RS256",
        }, new CustomResourceOptions { Parent = this });

        Audience = api.Identifier;
        RegisterOutputs();
    }
}
