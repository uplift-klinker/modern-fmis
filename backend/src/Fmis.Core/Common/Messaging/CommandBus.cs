using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public class CommandBus(IServiceProvider provider) : ICommandBus
{
    public async Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(command, cancellationToken);

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
        dynamic handler = provider.GetRequiredService(handlerType);
        return await handler.HandleAsync((dynamic)command, cancellationToken);
    }

    private async Task ValidateAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(command.GetType());
        if (provider.GetService(validatorType) is IValidator validator)
        {
            var context = new ValidationContext<object>(command);
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }
    }
}
