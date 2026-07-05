using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fmis.Models.Clients;
using Fmis.Models.Common;

namespace Fmis.Api.Tests.Clients;

public class ClientEndpointsTests(FmisApiFactory factory) : IClassFixture<FmisApiFactory>
{
    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "user");
        return client;
    }

    [Fact]
    public async Task Create_then_get_returns_the_created_client()
    {
        var client = CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("Acme Farms", "ops@acme.example", "+1 (555) 555-0100"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<ClientResponseModel>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);

        var get = await client.GetAsync($"/clients/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var fetched = await get.Content.ReadFromJsonAsync<ClientResponseModel>();
        Assert.Equal("Acme Farms", fetched!.Name);
    }

    [Fact]
    public async Task List_returns_created_clients_with_total_count()
    {
        var client = CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("Bedrock Ag", "info@bedrock.example", null));

        var list = await client.GetFromJsonAsync<ListResultModel<ClientResponseModel>>("/clients");

        Assert.NotNull(list);
        Assert.Contains(list!.Items, c => c.Name == "Bedrock Ag");
        Assert.True(list.TotalCount >= 1);
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/clients/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_blank_name_returns_400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("", "ops@acme.example", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_without_email_or_phone_returns_400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/clients",
            new CreateClientRequestModel("Acme Farms", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_authentication_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
