using System.Collections.Immutable;
using Pulumi.Testing;

namespace Fmis.Infra.Tests;

internal class StackMocks : IMocks
{
    public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
    {
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
