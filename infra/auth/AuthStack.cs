using Pulumi;
using Fmis.Infra.Common;
using Fmis.Infra.Auth.Components;

namespace Fmis.Infra.Auth;

public class AuthStack : Stack
{
    [Output("domain")] public Output<string> Domain { get; private set; }
    [Output("audience")] public Output<string> Audience { get; private set; }
    [Output("e2eClientId")] public Output<string?> E2eClientId { get; private set; }
    [Output("e2eClientSecret")] public Output<string?> E2eClientSecret { get; private set; }
    [Output("e2eUsername")] public Output<string?> E2eUsername { get; private set; }
    [Output("e2ePassword")] public Output<string?> E2ePassword { get; private set; }

    public AuthStack()
    {
        var env = Deployment.Instance.StackName;

        var api = new AuthApi(ResourceNames.For(env, "auth", "api"), ResourceNames.Audience(env));
        _ = new TenantConfiguration(ResourceNames.For(env, "auth", "tenant"));

        Domain = Output.Create(
            Environment.GetEnvironmentVariable("AUTH0_DOMAIN")
            ?? throw new InvalidOperationException("AUTH0_DOMAIN environment variable is required."));
        Audience = api.Audience;

        E2eClientId = Output.CreateSecret((string?)null);
        E2eClientSecret = Output.CreateSecret((string?)null);
        E2eUsername = Output.CreateSecret((string?)null);
        E2ePassword = Output.CreateSecret((string?)null);

        if (new Config().GetBoolean("enableE2eUser") ?? false)
        {
            var e2e = new E2eTestAccess(ResourceNames.For(env, "auth", "e2e"));
            E2eClientId = Output.CreateSecret(e2e.ClientId.Apply(value => (string?)value));
            E2eClientSecret = Output.CreateSecret(e2e.ClientSecret.Apply(value => (string?)value));
            E2eUsername = Output.CreateSecret(e2e.Username.Apply(value => (string?)value));
            E2ePassword = Output.CreateSecret(e2e.Password.Apply(value => (string?)value));
        }
    }
}
