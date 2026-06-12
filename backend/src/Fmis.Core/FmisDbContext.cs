using Fmis.Core.Clients;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core;

public class FmisDbContext(DbContextOptions<FmisDbContext> options) : DbContext(options)
{
    public DbSet<ClientEntity> Clients => Set<ClientEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FmisDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
