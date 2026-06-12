using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fmis.Core;

public class FmisDbContextFactory : IDesignTimeDbContextFactory<FmisDbContext>
{
    public FmisDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FmisDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=fmis;Username=fmis;Password=fmis")
            .Options;
        return new FmisDbContext(options);
    }
}
