using System.Collections.Immutable;
using Pulumi.Testing;

namespace Fmis.Infra.Tests;

internal class StackMocks : IMocks
{
    public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
    {
        if (args.Type == "pulumi:pulumi:StackReference")
        {
            var outputs = args.Name.Contains("auth")
                ? new Dictionary<string, object>
                {
                    ["domain"] = "fmis-dev.us.auth0.com",
                    ["spaClientId"] = "spa-client-id",
                    ["audience"] = "https://dev.api.modern-fmis",
                }
                : new Dictionary<string, object>
                {
                    ["serverFqdn"] = "fmis-dev-persistence-postgres.postgres.database.azure.com",
                    ["databaseName"] = "fmis",
                    ["appIdentityClientId"] = "00000000-0000-0000-0000-000000000001",
                    ["appIdentityPrincipalId"] = "00000000-0000-0000-0000-000000000002",
                    ["appIdentityName"] = "fmis-dev-app-identity",
                };
            var refState = args.Inputs.ToBuilder();
            refState["outputs"] = outputs;
            refState["secretOutputNames"] = ImmutableArray<string>.Empty;
            return Task.FromResult<(string?, object)>(($"{args.Name}_id", refState.ToImmutable()));
        }

        var state = args.Inputs.ToBuilder();
        foreach (var key in new[] { "resourceName", "configurationName", "databaseName", "serverName" })
        {
            if (state.TryGetValue(key, out var value))
            {
                state["name"] = value;
                if (key == "serverName")
                    state["fullyQualifiedDomainName"] = $"{value}.postgres.database.azure.com";
                break;
            }
        }
        AddProviderState(state, args);
        return Task.FromResult<(string?, object)>(($"{args.Name}_id", state.ToImmutable()));
    }

    public Task<object> CallAsync(MockCallArgs args)
        => Task.FromResult<object>(args.Args);

    public void RegisterResourceOutputs(MockRegisterResourceOutputsRequest request) { }

    protected virtual void AddProviderState(ImmutableDictionary<string, object>.Builder state, MockResourceArgs args) { }
}

internal sealed class AuthStackMocks : StackMocks
{
    protected override void AddProviderState(ImmutableDictionary<string, object>.Builder state, MockResourceArgs args)
    {
        state["clientId"] = $"{args.Name}_client_id";
        state["clientSecret"] = $"{args.Name}_clientSecret";
        state["result"] = $"{args.Name}_result";
    }
}
