using System.Net;

namespace Fmis.Api.Tests;

public class OpenApiTests(FmisApiFactory factory) : IClassFixture<FmisApiFactory>
{
    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/clients", body);
    }
}
