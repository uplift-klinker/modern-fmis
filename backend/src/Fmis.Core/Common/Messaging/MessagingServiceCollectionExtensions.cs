using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<ICommandBus, CommandBus>();
        services.AddScoped<IQueryBus, QueryBus>();

        foreach (var assembly in assemblies)
        {
            RegisterImplementations(services, assembly, typeof(ICommandHandler<,>));
            RegisterImplementations(services, assembly, typeof(IQueryHandler<,>));
        }

        return services;
    }

    private static void RegisterImplementations(IServiceCollection services, Assembly assembly, Type openHandlerInterface)
    {
        var types = assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false });
        foreach (var type in types)
        {
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerInterface);

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddScoped(handlerInterface, type);
            }
        }
    }
}
