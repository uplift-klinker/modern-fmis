using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Tests;

public class ApplicationStackTests
{
    [Fact]
    public async Task Creates_a_basic_container_registry()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var registry = resources.OfType<AzureNative.ContainerRegistry.Registry>().Single();
        Assert.Equal("Basic", await InfraTesting.GetAsync(registry.Sku.Apply(s => s.Name)));
    }

    [Fact]
    public async Task Runs_an_externally_ingressed_scale_to_zero_backend_app()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var app = resources.OfType<AzureNative.App.ContainerApp>().Single();
        Assert.Equal(8080, await InfraTesting.GetAsync(app.Configuration.Apply(c => c!.Ingress!.TargetPort!.Value)));
        Assert.Equal(0, await InfraTesting.GetAsync(app.Template.Apply(t => t!.Scale!.MinReplicas!.Value)));
    }

    [Fact]
    public async Task Injects_db_auth0_and_cors_settings_into_the_backend()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var app = resources.OfType<AzureNative.App.ContainerApp>().Single();
        var envVars = await InfraTesting.GetAsync(app.Template.Apply(t =>
            t!.Containers![0].Env!.ToDictionary(e => e.Name!, e => e.Value)));
        Assert.Equal("true", envVars["Database__UseEntraAuth"]);
        Assert.Contains("fmis-dev-persistence-postgres.postgres.database.azure.com", envVars["ConnectionStrings__Fmis"]);
        Assert.Contains("Username=fmis-dev-app-identity", envVars["ConnectionStrings__Fmis"]);
        Assert.DoesNotContain("Password=", envVars["ConnectionStrings__Fmis"]);
        Assert.Equal("https://fmis-dev.us.auth0.com/", envVars["Auth0__Authority"]);
        Assert.Equal("https://dev.api.modern-fmis", envVars["Auth0__Audience"]);
        Assert.Equal("00000000-0000-0000-0000-000000000001", envVars["AZURE_CLIENT_ID"]);
    }
}
