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
}
