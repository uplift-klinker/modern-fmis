namespace Fmis.Core.Common.Messaging;

public interface IQueryBus
{
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
