using Pulumi;
using Auth0 = Pulumi.Auth0;
using Fmis.Infra.Common;

namespace Fmis.Infra.Auth;

public class AuthStack : Stack
{
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
    }
}
