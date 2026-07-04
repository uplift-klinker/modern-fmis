using AzureNative = Pulumi.AzureNative;
using Auth0 = Pulumi.Auth0;
using Fmis.Infra.Application.Components;

namespace Fmis.Infra.Tests;

public class ApplicationStackTests
{
    [Fact]
    public async Task Builds_and_references_the_backend_image_from_the_registry()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        Assert.NotEmpty(resources.OfType<Pulumi.DockerBuild.Image>());
        var app = resources.OfType<AzureNative.App.ContainerApp>().Single();
        var image = await InfraTesting.GetAsync(app.Template.Apply(t => t!.Containers![0].Image!));
        Assert.Contains("fmis-backend", image);
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

    [Fact]
    public async Task Hosts_a_static_website_storage_account()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        Assert.NotEmpty(resources.OfType<AzureNative.Storage.StorageAccount>());
        Assert.NotEmpty(resources.OfType<AzureNative.Storage.StorageAccountStaticWebsite>());
    }

    [Fact]
    public async Task Permits_shared_key_uploads_to_the_public_static_site_account()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var account = resources.OfType<AzureNative.Storage.StorageAccount>().Single();
        Assert.True(await InfraTesting.GetAsync(account.AllowSharedKeyAccess));
    }

    [Fact]
    public async Task Writes_a_config_json_blob_with_the_spa_settings()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        Assert.NotEmpty(resources.OfType<AzureNative.Storage.Blob>());
        var site = resources.OfType<FrontendSite>().Single();
        var json = await InfraTesting.GetAsync(site.ConfigJson);
        Assert.Contains("\"apiBaseUrl\"", json);
        Assert.Contains("\"audience\"", json);
        Assert.Contains("\"clientId\"", json);
    }

    [Fact]
    public async Task Exposes_backend_and_frontend_urls()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();
        var stack = resources.OfType<Fmis.Infra.Application.ApplicationStack>().Single();

        Assert.NotNull(await InfraTesting.GetAsync(stack.BackendUrl));
        Assert.NotNull(await InfraTesting.GetAsync(stack.FrontendUrl));
    }

    [Fact]
    public async Task Creates_a_spa_client_allowing_localhost_and_the_deployed_site()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var spa = resources.OfType<Auth0.Client>().Single(c => InfraTesting.GetAsync(c.AppType).Result == "spa");
        var callbacks = await InfraTesting.GetAsync(spa.Callbacks);
        Assert.Contains("http://localhost:5173", callbacks);
        Assert.Contains("https://fmisdevweb.z00.web.core.windows.net/", callbacks);
    }

    [Fact]
    public async Task Declares_the_spa_as_a_public_client_with_no_token_endpoint_auth()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();

        var credentials = resources.OfType<Auth0.ClientCredentials>().Single();
        Assert.Equal("none", await InfraTesting.GetAsync(credentials.AuthenticationMethod));
    }

    [Fact]
    public async Task Exposes_the_spa_client_id_output()
    {
        var resources = await InfraTesting.RunApplicationStackAsync();
        var stack = resources.OfType<Fmis.Infra.Application.ApplicationStack>().Single();

        Assert.NotNull(await InfraTesting.GetAsync(stack.SpaClientId));
    }
}
