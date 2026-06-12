using FluentValidation;
using Fmis.Core.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Tests.Common;

public class CommandBusTests
{
    public record PingCommand(string Text) : ICommand<string>;

    public class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> HandleAsync(PingCommand command, CancellationToken cancellationToken)
            => Task.FromResult($"pong:{command.Text}");
    }

    public class PingCommandValidator : AbstractValidator<PingCommand>
    {
        public PingCommandValidator() => RuleFor(c => c.Text).NotEmpty();
    }

    [Fact]
    public async Task Resolves_and_invokes_the_registered_handler_via_DI()
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<ICommandBus>();
        var result = await bus.ExecuteAsync(new PingCommand("hi"));

        Assert.Equal("pong:hi", result);
    }

    [Fact]
    public async Task Validates_the_command_and_throws_when_invalid()
    {
        var services = new ServiceCollection();
        services.AddMessaging();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        services.AddScoped<IValidator<PingCommand>, PingCommandValidator>();
        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<ICommandBus>();

        await Assert.ThrowsAsync<ValidationException>(
            () => bus.ExecuteAsync(new PingCommand("")));
    }
}
