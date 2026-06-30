using Azure.Core;
using Azure.Identity;
using Pulumi;

namespace Fmis.Infra.Persistence;

public static class PostgresAdminToken
{
    public static Func<Output<string>> Provider { get; set; } = FromAzureIdentity;

    private static Output<string> FromAzureIdentity() =>
        Output.CreateSecret(Output.Create(FetchAsync()));

    private static async Task<string> FetchAsync()
    {
        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" }));
        return token.Token;
    }
}
