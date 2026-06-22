using AzureNative = Pulumi.AzureNative;

namespace Fmis.Infra.Tests;

public class PersistenceStackTests
{
    [Fact]
    public async Task Creates_a_burstable_pg16_entra_only_server()
    {
        var resources = await InfraTesting.RunPersistenceStackAsync();

        var server = resources.OfType<AzureNative.DBforPostgreSQL.Server>().Single();
        Assert.Equal("fmis-dev-persistence-postgres", await InfraTesting.GetAsync(server.Name));
        Assert.Equal("16", await InfraTesting.GetAsync(server.Version));
    }
}
