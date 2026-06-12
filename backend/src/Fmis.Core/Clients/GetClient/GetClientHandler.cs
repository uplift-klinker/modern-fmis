using Fmis.Core.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Fmis.Core.Clients.GetClient;

public class GetClientHandler(FmisDbContext db)
    : IQueryHandler<GetClientQuery, ClientResult?>
{
    public async Task<ClientResult?> HandleAsync(GetClientQuery query, CancellationToken cancellationToken)
    {
        return await db.Clients
            .Where(c => c.Id == query.Id)
            .Select(c => new ClientResult(c.Id, c.Name, c.Email, c.PhoneNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
