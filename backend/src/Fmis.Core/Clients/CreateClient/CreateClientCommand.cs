using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.CreateClient;

public record CreateClientCommand(string Name, string? Email, string? PhoneNumber)
    : ICommand<CreateClientResult>;
