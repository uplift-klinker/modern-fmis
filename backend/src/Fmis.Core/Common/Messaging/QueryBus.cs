using Microsoft.Extensions.DependencyInjection;

namespace Fmis.Core.Common.Messaging;

public class QueryBus(IServiceProvider provider) : IQueryBus
{
    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        dynamic handler = provider.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)query, cancellationToken);
    }
}
