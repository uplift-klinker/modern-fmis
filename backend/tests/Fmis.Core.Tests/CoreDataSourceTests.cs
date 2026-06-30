using Fmis.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests;

public class CoreDataSourceTests
{
    [Fact]
    public void Password_mode_resolves_the_db_context()
    {
        var services = new ServiceCollection();

        services.AddFmisCore(new FmisDatabaseOptions(
            "Host=localhost;Database=fmis;Username=fmis;Password=fmis", false, null));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<FmisDbContext>());
    }

    [Fact]
    public void Entra_mode_resolves_the_db_context_without_a_password()
    {
        var services = new ServiceCollection();

        services.AddFmisCore(new FmisDatabaseOptions(
            "Host=fmis-dev-persistence-postgres.postgres.database.azure.com;Database=fmis;Username=fmis-dev-app-identity;Ssl Mode=Require",
            true, "00000000-0000-0000-0000-000000000001"));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<FmisDbContext>());
    }
}
