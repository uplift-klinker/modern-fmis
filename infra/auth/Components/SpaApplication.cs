using Pulumi;
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Auth.Components;

public sealed class SpaApplication : ComponentResource
{
    public Output<string> ClientId { get; }

    public SpaApplication(string name, ComponentResourceOptions? options = null)
        : base("fmis:auth:SpaApplication", name, options)
    {
        var client = new Auth0.Client(name, new Auth0.ClientArgs
        {
            Name = name,
            AppType = "spa",
            OidcConformant = true,
            GrantTypes = { "authorization_code", "refresh_token" },
            Callbacks = { "http://localhost:5173" },
            AllowedLogoutUrls = { "http://localhost:5173" },
            WebOrigins = { "http://localhost:5173" },
        }, new CustomResourceOptions { Parent = this });

        ClientId = client.ClientId;
        RegisterOutputs();
    }
}
