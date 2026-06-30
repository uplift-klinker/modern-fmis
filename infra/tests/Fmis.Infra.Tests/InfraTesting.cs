using System.Collections.Immutable;
using Pulumi;
using Pulumi.Testing;

namespace Fmis.Infra.Tests;

internal static class InfraTesting
{
    public static async Task<ImmutableArray<Resource>> RunAuthStackAsync(bool enableE2eUser)
    {
        var previousConfig = Environment.GetEnvironmentVariable("PULUMI_CONFIG");
        var previousDomain = Environment.GetEnvironmentVariable("AUTH0_DOMAIN");
        var previousClientId = Environment.GetEnvironmentVariable("AUTH0_CLIENT_ID");
        var previousClientSecret = Environment.GetEnvironmentVariable("AUTH0_CLIENT_SECRET");

        Environment.SetEnvironmentVariable(
            "PULUMI_CONFIG",
            $$"""{"fmis-auth:enableE2eUser":"{{(enableE2eUser ? "true" : "false")}}"}""");
        Environment.SetEnvironmentVariable("AUTH0_DOMAIN", "dev.modern-fmis.auth0.com");
        Environment.SetEnvironmentVariable("AUTH0_CLIENT_ID", "test-client-id");
        Environment.SetEnvironmentVariable("AUTH0_CLIENT_SECRET", "test-client-secret");
        try
        {
            return await Deployment.TestAsync<Fmis.Infra.Auth.AuthStack>(
                new AuthStackMocks(),
                new TestOptions { StackName = "dev", ProjectName = "fmis-auth", IsPreview = false });
        }
        finally
        {
            Environment.SetEnvironmentVariable("PULUMI_CONFIG", previousConfig);
            Environment.SetEnvironmentVariable("AUTH0_DOMAIN", previousDomain);
            Environment.SetEnvironmentVariable("AUTH0_CLIENT_ID", previousClientId);
            Environment.SetEnvironmentVariable("AUTH0_CLIENT_SECRET", previousClientSecret);
        }
    }

    public static async Task<ImmutableArray<Resource>> RunPersistenceStackAsync()
    {
        var previousDeployerIp = Environment.GetEnvironmentVariable("DEPLOYER_IP");
        var previousAdminObjectId = Environment.GetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID");
        var previousTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var previousTokenProvider = Fmis.Infra.Persistence.PostgresAdminToken.Provider;

        Environment.SetEnvironmentVariable("DEPLOYER_IP", "203.0.113.10");
        Environment.SetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID", "00000000-0000-0000-0000-000000000001");
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", "00000000-0000-0000-0000-0000000000aa");
        Fmis.Infra.Persistence.PostgresAdminToken.Provider = () => Output.CreateSecret("test-token");
        try
        {
            return await Deployment.TestAsync<Fmis.Infra.Persistence.PersistenceStack>(
                new StackMocks(),
                new TestOptions { StackName = "dev", ProjectName = "fmis-persistence", IsPreview = false });
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEPLOYER_IP", previousDeployerIp);
            Environment.SetEnvironmentVariable("DEPLOY_PRINCIPAL_OBJECT_ID", previousAdminObjectId);
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", previousTenantId);
            Fmis.Infra.Persistence.PostgresAdminToken.Provider = previousTokenProvider;
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
