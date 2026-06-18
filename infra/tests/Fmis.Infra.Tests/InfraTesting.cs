using System.Collections.Immutable;
using Pulumi;
using Pulumi.Testing;

namespace Fmis.Infra.Tests;

internal sealed class StackMocks : IMocks
{
    public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
    {
        var state = args.Inputs.ToBuilder();
        state["clientId"] = $"{args.Name}_client_id";
        return Task.FromResult<(string?, object)>(($"{args.Name}_id", state.ToImmutable()));
    }

    public Task<object> CallAsync(MockCallArgs args)
        => Task.FromResult<object>(args.Args);

    public void RegisterResourceOutputs(MockRegisterResourceOutputsRequest request) { }
}

internal static class InfraTesting
{
    public static async Task<ImmutableArray<Resource>> RunAuthStackAsync(bool enableE2eUser)
    {
        var previous = Environment.GetEnvironmentVariable("PULUMI_CONFIG");
        Environment.SetEnvironmentVariable(
            "PULUMI_CONFIG",
            $$"""{"fmis-auth:enableE2eUser":"{{(enableE2eUser ? "true" : "false")}}","auth0:domain":"dev.modern-fmis.auth0.com","auth0:clientId":"test-client-id","auth0:clientSecret":"test-client-secret"}""");
        try
        {
            return await Deployment.TestAsync<Fmis.Infra.Auth.AuthStack>(
                new StackMocks(),
                new TestOptions { StackName = "dev", ProjectName = "fmis-auth", IsPreview = false });
        }
        finally
        {
            Environment.SetEnvironmentVariable("PULUMI_CONFIG", previous);
        }
    }

    public static Task<T> GetAsync<T>(Output<T> output)
    {
        var completion = new TaskCompletionSource<T>();
        output.Apply(value =>
        {
            completion.SetResult(value);
            return value;
        });
        return completion.Task;
    }
}
