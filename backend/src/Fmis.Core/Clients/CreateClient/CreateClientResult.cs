namespace Fmis.Core.Clients.CreateClient;

public record CreateClientResult(Guid Id, string Name, string? Email, string? PhoneNumber);
