using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace Fmis.Core;

public record FmisDatabaseOptions(string ConnectionString, bool UseEntraAuth, string? ManagedIdentityClientId);

public static class FmisDataSource
{
    private static readonly string[] Scope = ["https://ossrdbms-aad.database.windows.net/.default"];

    public static NpgsqlDataSource Build(FmisDatabaseOptions options)
    {
        var builder = new NpgsqlDataSourceBuilder(options.ConnectionString);
        if (options.UseEntraAuth)
        {
            var credential = new DefaultAzureCredential(
                new DefaultAzureCredentialOptions { ManagedIdentityClientId = options.ManagedIdentityClientId });
            builder.UsePeriodicPasswordProvider(
                async (_, cancellationToken) =>
                {
                    var token = await credential.GetTokenAsync(new TokenRequestContext(Scope), cancellationToken);
                    return token.Token;
                },
                TimeSpan.FromMinutes(55),
                TimeSpan.FromSeconds(5));
        }
        return builder.Build();
    }
}
