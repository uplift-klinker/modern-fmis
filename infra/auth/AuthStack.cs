using Pulumi;
using Auth0 = Pulumi.Auth0;
using Fmis.Infra.Common;
using Random = Pulumi.Random;

namespace Fmis.Infra.Auth;

public class AuthStack : Stack
{
    [Output("domain")] public Output<string> Domain { get; private set; }
    [Output("spaClientId")] public Output<string> SpaClientId { get; private set; }
    [Output("audience")] public Output<string> Audience { get; private set; }
    [Output("e2eClientId")] public Output<string?> E2eClientId { get; private set; }
    [Output("e2eClientSecret")] public Output<string?> E2eClientSecret { get; private set; }
    [Output("e2eUsername")] public Output<string?> E2eUsername { get; private set; }
    [Output("e2ePassword")] public Output<string?> E2ePassword { get; private set; }

    public AuthStack()
    {
        var env = Deployment.Instance.StackName;

        var spaName = ResourceNames.For(env, "auth", "spa");
        var spa = new Auth0.Client(spaName, new Auth0.ClientArgs
        {
            Name = spaName,
            AppType = "spa",
            OidcConformant = true,
            GrantTypes = { "authorization_code", "refresh_token" },
            Callbacks = { "http://localhost:5173" },
            AllowedLogoutUrls = { "http://localhost:5173" },
            WebOrigins = { "http://localhost:5173" },
        });

        var apiName = ResourceNames.For(env, "auth", "api");
        var api = new Auth0.ResourceServer(apiName, new Auth0.ResourceServerArgs
        {
            Name = apiName,
            Identifier = ResourceNames.Audience(env),
            SigningAlg = "RS256",
        });

        var tenant = new Auth0.Tenant(ResourceNames.For(env, "auth", "tenant"), new Auth0.TenantArgs
        {
            DefaultDirectory = "Username-Password-Authentication",
        });

        var auth0Config = new Config("auth0");

        Domain = Output.Create(auth0Config.Require("domain"));
        SpaClientId = spa.ClientId!;
        Audience = api.Identifier!;

        E2eClientId = Output.Create((string?)null);
        E2eClientSecret = Output.CreateSecret((string?)null);
        E2eUsername = Output.CreateSecret((string?)null);
        E2ePassword = Output.CreateSecret((string?)null);

        var config = new Config();
        if (config.GetBoolean("enableE2eUser") ?? false)
        {
            var password = new Random.RandomPassword(ResourceNames.For(env, "auth", "e2e-password"),
                new Random.RandomPasswordArgs { Length = 24, Special = true });

            var user = new Auth0.User(ResourceNames.For(env, "auth", "e2e-user"), new Auth0.UserArgs
            {
                ConnectionName = "Username-Password-Authentication",
                Email = "e2e@dev.modern-fmis.test",
                EmailVerified = true,
                Password = password.Result,
            });

            var e2eName = ResourceNames.For(env, "auth", "e2e");
            var e2eClient = new Auth0.Client(e2eName, new Auth0.ClientArgs
            {
                Name = e2eName,
                AppType = "non_interactive",
                OidcConformant = true,
                GrantTypes = { "password", "http://auth0.com/oauth/grant-type/password-realm" },
            });

            E2eClientId = e2eClient.ClientId!;
            E2eUsername = Output.CreateSecret(user.Email!);
            E2ePassword = Output.CreateSecret(password.Result);
        }
    }
}
