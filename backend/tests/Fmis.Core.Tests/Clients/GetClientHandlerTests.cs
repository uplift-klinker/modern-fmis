using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.GetClient;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class GetClientHandlerTests : InMemoryCoreTestBase
{
    [Fact]
    public async Task Returns_the_client_when_it_exists()
    {
        var created = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "ops@acme.example", null));

        var result = await QueryBus.QueryAsync(new GetClientQuery(created.Id));

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
        Assert.Equal("Acme Farms", result.Name);
    }

    [Fact]
    public async Task Returns_null_when_the_client_does_not_exist()
    {
        var result = await QueryBus.QueryAsync(new GetClientQuery(Guid.NewGuid()));

        Assert.Null(result);
    }
}
