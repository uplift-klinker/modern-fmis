using Pulumi;
using Auth0 = Pulumi.Auth0;
using Random = Pulumi.Random;

namespace Fmis.Infra.Auth.Components;

public sealed class E2eTestAccess : ComponentResource
{
    public Output<string> ClientId { get; }
    public Output<string> ClientSecret { get; }
    public Output<string> Username { get; }
    public Output<string> Password { get; }

    public E2eTestAccess(string name, ComponentResourceOptions? options = null)
        : base("fmis:auth:E2eTestAccess", name, options)
    {
        var childOptions = new CustomResourceOptions { Parent = this };

        var password = new Random.RandomPassword($"{name}-password",
            new Random.RandomPasswordArgs { Length = 24, Special = true }, childOptions);

        var user = new Auth0.User($"{name}-user", new Auth0.UserArgs
        {
            ConnectionName = "Username-Password-Authentication",
            Email = "e2e@dev.modern-fmis.test",
            EmailVerified = true,
            Password = password.Result,
        }, childOptions);

        var client = new Auth0.Client(name, new Auth0.ClientArgs
        {
            Name = name,
            AppType = "non_interactive",
            OidcConformant = true,
            GrantTypes = { "password", "http://auth0.com/oauth/grant-type/password-realm" },
        }, childOptions);

        var credentials = new Auth0.ClientCredentials($"{name}-creds", new Auth0.ClientCredentialsArgs
        {
            ClientId = client.ClientId,
            AuthenticationMethod = "client_secret_post",
        }, childOptions);

        ClientId = client.ClientId;
        ClientSecret = credentials.ClientSecret;
        Username = user.Email.Apply(v => v ?? string.Empty);
        Password = password.Result;
        RegisterOutputs();
    }
}
