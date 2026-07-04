using Pulumi;
using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Application.Components;

public sealed class SpaClient : ComponentResource
{
    public Output<string> ClientId { get; }

    public SpaClient(string name, Input<string> frontendUrl, ComponentResourceOptions? options = null)
        : base("fmis:application:SpaClient", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var callbacks = frontendUrl.ToOutput().Apply(url => new[] { "http://localhost:5173", url });

        var client = new Auth0.Client(name, new Auth0.ClientArgs
        {
            Name = name,
            AppType = "spa",
            OidcConformant = true,
            GrantTypes = { "authorization_code", "refresh_token" },
            Callbacks = callbacks,
            AllowedLogoutUrls = callbacks,
            WebOrigins = callbacks,
        }, childOptions);

        _ = new Auth0.ClientCredentials($"{name}-creds", new Auth0.ClientCredentialsArgs
        {
            ClientId = client.ClientId,
            AuthenticationMethod = "none",
        }, childOptions);

        ClientId = client.ClientId;
        RegisterOutputs();
    }
}
