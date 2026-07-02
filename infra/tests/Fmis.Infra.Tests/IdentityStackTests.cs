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

    [Fact]
    public async Task Grants_the_identity_pull_and_the_deployer_push_on_the_acr()
    {
        var resources = await InfraTesting.RunIdentityStackAsync();

        var roles = resources.OfType<AzureNative.Authorization.RoleAssignment>().ToList();
        Assert.Equal(2, roles.Count);
        var roleDefs = await Task.WhenAll(roles.Select(r => InfraTesting.GetAsync(r.RoleDefinitionId)));
        Assert.Contains(roleDefs, d => d.Contains("7f951dda-4ed3-4680-a7ca-43fe172d538d"));
        Assert.Contains(roleDefs, d => d.Contains("8311e382-0749-4cb8-b61a-304f252e45ec"));
    }

    [Fact]
    public async Task Provisions_the_entra_principal_and_grants()
    {
        var resources = await InfraTesting.RunIdentityStackAsync();

        Assert.NotEmpty(resources.OfType<Pulumi.Command.Local.Command>());
        var grant = resources.OfType<Pulumi.PostgreSql.Grant>().Single();
        Assert.Equal("fmis", await InfraTesting.GetAsync(grant.Database));
    }
}
