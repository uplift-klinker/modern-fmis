namespace Fmis.Core.Common.Messaging;

public interface ICommandBus
{
    Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
}
