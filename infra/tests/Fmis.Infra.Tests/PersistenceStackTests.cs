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

    [Fact]
    public async Task Sets_the_ci_principal_as_the_entra_admin()
    {
        var resources = await InfraTesting.RunPersistenceStackAsync();

        var admin = resources.OfType<AzureNative.DBforPostgreSQL.Administrator>().Single();
        Assert.Equal("00000000-0000-0000-0000-000000000001", await InfraTesting.GetAsync(admin.ObjectId));
    }

    [Fact]
    public async Task Locks_the_server_against_deletion()
    {
        var resources = await InfraTesting.RunPersistenceStackAsync();

        var locks = resources.OfType<AzureNative.Authorization.ManagementLockByScope>().ToList();
        Assert.Contains(locks, l => InfraTesting.GetAsync(l.Level).Result == "CanNotDelete");
    }
}
