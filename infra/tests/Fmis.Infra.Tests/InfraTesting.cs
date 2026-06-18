using System.Collections.Immutable;
using Pulumi;
using Pulumi.Testing;

namespace Fmis.Infra.Tests;

internal sealed class StackMocks : IMocks
{
    public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
        => Task.FromResult<(string?, object)>(($"{args.Name}_id", args.Inputs));

    public Task<object> CallAsync(MockCallArgs args)
        => Task.FromResult<object>(args.Args);

    public void RegisterResourceOutputs(MockRegisterResourceOutputsRequest request) { }
}

internal static class InfraTesting
{
    public static Task<ImmutableArray<Resource>> RunAuthStackAsync(bool enableE2eUser)
    {
        Environment.SetEnvironmentVariable(
            "PULUMI_CONFIG",
            $$"""{"fmis-auth:enableE2eUser":"{{(enableE2eUser ? "true" : "false")}}","auth0:domain":"dev.modern-fmis.auth0.com","auth0:clientId":"test-client-id","auth0:clientSecret":"test-client-secret"}""");

        return Deployment.TestAsync<Fmis.Infra.Auth.AuthStack>(
            new StackMocks(),
            new TestOptions { StackName = "dev", ProjectName = "fmis-auth", IsPreview = false });
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
