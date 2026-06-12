using Fmis.Core.Common;
using Fmis.Core.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.ListClients;

public class ListClientsHandler(FmisDbContext db)
    : IQueryHandler<ListClientsQuery, ListResult<ClientResult>>
{
    public async Task<ListResult<ClientResult>> HandleAsync(ListClientsQuery query, CancellationToken cancellationToken)
    {
        var items = await db.Clients
            .OrderBy(c => c.Name)
            .Select(c => new ClientResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .ToListAsync(cancellationToken);

        var totalCount = await db.Clients.CountAsync(cancellationToken);

        return new ListResult<ClientResult>(items, totalCount);
    }
}
