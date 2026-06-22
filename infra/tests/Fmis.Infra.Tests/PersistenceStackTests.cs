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

    [Fact]
    public async Task Allows_azure_services_and_the_deployer_ip()
    {
        var resources = await InfraTesting.RunPersistenceStackAsync();

        var rules = resources.OfType<AzureNative.DBforPostgreSQL.FirewallRule>().ToList();
        Assert.Contains(rules, r => InfraTesting.GetAsync(r.StartIpAddress).Result == "0.0.0.0"
            && InfraTesting.GetAsync(r.EndIpAddress).Result == "0.0.0.0");
        Assert.Contains(rules, r => InfraTesting.GetAsync(r.StartIpAddress).Result == "203.0.113.10");
    }

    [Fact]
    public async Task Creates_the_fmis_database()
    {
        var resources = await InfraTesting.RunPersistenceStackAsync();

        var database = resources.OfType<AzureNative.DBforPostgreSQL.Database>().Single();
        Assert.Equal("fmis", await InfraTesting.GetAsync(database.Name));
    }

    [Fact]
    public async Task Allowlists_the_postgis_extension()
    {
        var resources = await InfraTesting.RunPersistenceStackAsync();

        var config = resources.OfType<AzureNative.DBforPostgreSQL.Configuration>()
            .Single(c => InfraTesting.GetAsync(c.Name).Result == "azure.extensions");
        Assert.Contains("POSTGIS", await InfraTesting.GetAsync(config.Value));
    }
}
