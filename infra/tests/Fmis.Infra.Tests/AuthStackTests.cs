using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Tests;

public class AuthStackTests
{
    [Fact]
    public async Task Creates_the_spa_application_named_for_the_environment()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        var clients = resources.OfType<Auth0.Client>().ToList();
        var spa = clients.Single(c => InfraTesting.GetAsync(c.AppType).Result == "spa");
        Assert.Equal("fmis-dev-auth-spa", await InfraTesting.GetAsync(spa.Name));
    }

    [Fact]
    public async Task Creates_the_api_resource_server_with_the_env_named_audience()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        var api = resources.OfType<Auth0.ResourceServer>().Single();
        Assert.Equal("fmis-dev-auth-api", await InfraTesting.GetAsync(api.Name));
        Assert.Equal("https://dev.api.modern-fmis", await InfraTesting.GetAsync(api.Identifier));
    }

    [Fact]
    public async Task Sets_the_tenant_default_directory_to_the_database_connection()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        var tenant = resources.OfType<Auth0.Tenant>().Single();
        Assert.Equal("Username-Password-Authentication", await InfraTesting.GetAsync(tenant.DefaultDirectory));
    }
}
