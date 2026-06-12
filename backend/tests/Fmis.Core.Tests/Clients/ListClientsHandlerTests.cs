using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.ListClients;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class ListClientsHandlerTests : InMemoryCoreTestBase
{
    [Fact]
    public async Task Returns_all_clients_with_total_count()
    {
        await CommandBus.ExecuteAsync(new CreateClientCommand("Acme Farms", "ops@acme.example", null));
        await CommandBus.ExecuteAsync(new CreateClientCommand("Bedrock Ag", "info@bedrock.example", null));

        var result = await QueryBus.QueryAsync(new ListClientsQuery());

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, r => r.Name == "Acme Farms");
        Assert.Contains(result.Items, r => r.Name == "Bedrock Ag");
    }

    [Fact]
    public async Task Returns_empty_with_zero_total_when_there_are_no_clients()
    {
        var result = await QueryBus.QueryAsync(new ListClientsQuery());

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }
}
