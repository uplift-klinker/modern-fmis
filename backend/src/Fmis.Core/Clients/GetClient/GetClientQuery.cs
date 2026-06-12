using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.GetClient;

public record GetClientQuery(Guid Id) : IQuery<ClientResult?>;
