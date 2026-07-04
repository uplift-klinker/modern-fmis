using FluentValidation;
using Fmis.Core.Clients.CreateClient;
using Fmis.TestSupport;

namespace Fmis.Core.Tests.Clients;

public class CreateClientHandlerTests : InMemoryCoreTestBase
{
    [Fact]
    public async Task Persists_the_client_and_returns_it_with_a_generated_id()
    {
        var result = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "ops@acme.example", "(555) 555-0100"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Acme Farms", result.Name);
        Assert.Equal("ops@acme.example", result.Email);
        Assert.Equal("(555) 555-0100", result.PhoneNumber);

        var saved = Assert.Single(Db.Clients);
        Assert.Equal(result.Id, saved.Id);
    }

    [Fact]
    public async Task Accepts_a_client_with_only_a_phone_number()
    {
        var result = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", null, "(555) 555-0100"));

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Accepts_a_client_with_only_an_email()
    {
        var result = await CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "ops@acme.example", null));

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Rejects_a_blank_name()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CommandBus.ExecuteAsync(
            new CreateClientCommand("", "ops@acme.example", null)));
    }

    [Fact]
    public async Task Rejects_a_client_with_no_email_or_phone()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", null, null)));
    }

    [Fact]
    public async Task Rejects_a_malformed_email()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", "a", null)));
    }

    [Fact]
    public async Task Rejects_a_phone_number_that_is_too_short()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CommandBus.ExecuteAsync(
            new CreateClientCommand("Acme Farms", null, "555")));
    }
}
