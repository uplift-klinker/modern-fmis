namespace Fmis.Models.Clients;

public record ClientResponseModel(Guid Id, string Name, string? Email, string? PhoneNumber);
