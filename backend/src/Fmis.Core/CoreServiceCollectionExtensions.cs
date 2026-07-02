using Fmis.Core.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddFmisCore(this IServiceCollection services, FmisDatabaseOptions options)
    {
        var dataSource = FmisDataSource.Build(options);
        services.AddDbContext<FmisDbContext>(dbOptions => dbOptions.UseNpgsql(dataSource));
        return services.AddFmisCoreHandlers();
    }

    public static IServiceCollection AddFmisCoreHandlers(this IServiceCollection services)
    {
        var coreAssembly = typeof(CoreServiceCollectionExtensions).Assembly;
        services.AddMessaging(coreAssembly);
        services.AddValidatorsFromAssembly(coreAssembly);
        return services;
    }
}
