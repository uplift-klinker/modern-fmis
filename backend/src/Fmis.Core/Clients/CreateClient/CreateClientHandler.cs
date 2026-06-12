using Fmis.Core.Common.Messaging;

namespace Fmis.Core.Clients.CreateClient;

public class CreateClientHandler(FmisDbContext db)
    : ICommandHandler<CreateClientCommand, CreateClientResult>
{
    public async Task<CreateClientResult> HandleAsync(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var client = new ClientEntity
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Email = command.Email,
            PhoneNumber = command.PhoneNumber,
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateClientResult(client.Id, client.Name, client.Email, client.PhoneNumber);
    }
}
