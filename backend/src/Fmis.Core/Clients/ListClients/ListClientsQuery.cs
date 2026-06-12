using Fmis.Core.Common;
using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.ListClients;

public record ListClientsQuery : IQuery<ListResult<ClientResult>>;
