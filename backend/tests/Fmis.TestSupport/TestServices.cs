using Fmis.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.TestSupport;

public static class TestServices
{
    public static ServiceProvider CreateInMemory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<FmisDbContext>(options =>
            options.UseInMemoryDatabase($"fmis-test-{Guid.NewGuid()}"));
        services.AddFmisCoreHandlers();
        return services.BuildServiceProvider();
    }
}
