using Fmis.Core.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddFmisCore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FmisDbContext>(options => options.UseNpgsql(connectionString));
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
