using Fmis.Api.Common;
using Fmis.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace Fmis.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFmisCore(new Fmis.Core.FmisDatabaseOptions(
            configuration.GetConnectionString("Fmis") ?? throw new InvalidOperationException("Missing connection string 'Fmis'."),
            configuration.GetValue("Database:UseEntraAuth", false),
            configuration["AZURE_CLIENT_ID"]));
        services.AddApiControllers();
        services.AddApiErrorHandling();
        services.AddApiDocumentation();
        services.AddApiAuthentication(configuration);
        return services;
    }

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);
        return services;
    }

    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Auth0:Authority"];
                options.Audience = configuration["Auth0:Audience"];
            });
        services.AddAuthorization();
        return services;
    }
}
