using Fmis.Core;
using Fmis.Core.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Fmis.TestSupport;

public abstract class InMemoryCoreTestBase : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    protected InMemoryCoreTestBase()
    {
        _provider = TestServices.CreateInMemory();
        _scope = _provider.CreateScope();
    }

    protected IServiceProvider Services => _scope.ServiceProvider;
    protected ICommandBus CommandBus => Services.GetRequiredService<ICommandBus>();
    protected IQueryBus QueryBus => Services.GetRequiredService<IQueryBus>();
    protected FmisDbContext Db => Services.GetRequiredService<FmisDbContext>();

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }
}
