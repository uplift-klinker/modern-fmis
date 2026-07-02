using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Tests;

public class IdentityStackTests
{
    [Fact]
    public async Task Creates_the_app_managed_identity()
    {
        var resources = await InfraTesting.RunIdentityStackAsync();

        var identity = resources.OfType<AzureNative.ManagedIdentity.UserAssignedIdentity>().Single();
        Assert.Equal("fmis-dev-app-identity", await InfraTesting.GetAsync(identity.Name));
    }
}
