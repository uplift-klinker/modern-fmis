using Fmis.Api.Configuration;
using Fmis.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Api.Tests;

public class FmisApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"fmis-api-tests-{Guid.NewGuid()}";
    private readonly Dictionary<string, string?> overrides = new();

    public FmisApiFactory WithConfig(string key, string value)
    {
        overrides[key] = value;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(overrides));
        builder.ConfigureTestServices(services =>
        {
            RemoveEntityFrameworkServices(services);
            services.AddDbContext<FmisDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            if (overrides.Count > 0)
            {
                var config = new ConfigurationBuilder().AddInMemoryCollection(overrides).Build();
                services.AddApiCors(config);
            }
        });
    }

    private static void RemoveEntityFrameworkServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(descriptor => descriptor.ServiceType.Namespace is { } ns
                && (ns.StartsWith("Microsoft.EntityFrameworkCore") || ns.StartsWith("Npgsql")))
            .ToList();

        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
