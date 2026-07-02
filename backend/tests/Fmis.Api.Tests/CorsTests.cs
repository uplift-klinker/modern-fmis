using System.Net.Http;

namespace Fmis.Api.Tests;

public class CorsTests
{
    [Fact]
    public async Task Allows_the_configured_spa_origin()
    {
        await using var factory = new FmisApiFactory()
            .WithConfig("Cors:AllowedOrigin", "https://fmisdevweb.z13.web.core.windows.net");
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/clients");
        request.Headers.Add("Origin", "https://fmisdevweb.z13.web.core.windows.net");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await client.SendAsync(request);

        Assert.Contains("https://fmisdevweb.z13.web.core.windows.net",
            response.Headers.GetValues("Access-Control-Allow-Origin"));
    }
}
