using Auth0 = Pulumi.Auth0;

namespace Fmis.Infra.Tests;

public class AuthStackTests
{
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

    [Fact]
    public async Task Provisions_the_e2e_user_and_client_when_enabled()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: true);

        var user = resources.OfType<Auth0.User>().Single();
        Assert.Equal("Username-Password-Authentication", await InfraTesting.GetAsync(user.ConnectionName));

        var e2eClient = resources.OfType<Auth0.Client>()
            .Single(c => InfraTesting.GetAsync(c.Name).Result == "fmis-dev-auth-e2e");
        var grantTypes = await InfraTesting.GetAsync(e2eClient.GrantTypes);
        Assert.Contains("password", grantTypes);
    }

    [Fact]
    public async Task Omits_the_e2e_user_and_client_when_disabled()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        Assert.Empty(resources.OfType<Auth0.User>());
        Assert.DoesNotContain(
            resources.OfType<Auth0.Client>(),
            c => InfraTesting.GetAsync(c.Name).Result == "fmis-dev-auth-e2e");
    }

    [Fact]
    public async Task Exposes_domain_and_audience_outputs()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);
        var stack = resources.OfType<Fmis.Infra.Auth.AuthStack>().Single();

        Assert.Equal("dev.modern-fmis.auth0.com", await InfraTesting.GetAsync(stack.Domain));
        Assert.Equal("https://dev.api.modern-fmis", await InfraTesting.GetAsync(stack.Audience));
    }

    [Fact]
    public async Task Does_not_create_a_spa_application()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);

        Assert.DoesNotContain(
            resources.OfType<Auth0.Client>(),
            c => InfraTesting.GetAsync(c.AppType).Result == "spa");
    }

    [Fact]
    public async Task Exposes_the_e2e_credentials_when_enabled()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: true);
        var stack = resources.OfType<Fmis.Infra.Auth.AuthStack>().Single();

        Assert.NotNull(await InfraTesting.GetAsync(stack.E2eClientId));
        Assert.NotNull(await InfraTesting.GetAsync(stack.E2eClientSecret));
        Assert.NotNull(await InfraTesting.GetAsync(stack.E2eUsername));
        Assert.NotNull(await InfraTesting.GetAsync(stack.E2ePassword));
    }

    [Fact]
    public async Task E2e_outputs_are_null_when_disabled()
    {
        var resources = await InfraTesting.RunAuthStackAsync(enableE2eUser: false);
        var stack = resources.OfType<Fmis.Infra.Auth.AuthStack>().Single();

        Assert.Null(await InfraTesting.GetAsync(stack.E2eClientId));
        Assert.Null(await InfraTesting.GetAsync(stack.E2eClientSecret));
    }

}
