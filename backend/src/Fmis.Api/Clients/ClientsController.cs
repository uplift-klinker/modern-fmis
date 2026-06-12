using Fmis.Core.Clients.CreateClient;
using Fmis.Core.Clients.GetClient;
using Fmis.Core.Clients.ListClients;
using Fmis.Core.Common.Messaging;
using Fmis.Models.Clients;
using Fmis.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fmis.Api.Clients;

[ApiController]
[Route("clients")]
[Authorize]
public class ClientsController(ICommandBus commandBus, IQueryBus queryBus) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ClientResponseModel>> Create(
        [FromBody] CreateClientRequestModel request, CancellationToken cancellationToken)
    {
        var result = await commandBus.ExecuteAsync(
            new CreateClientCommand(request.Name, request.Email, request.PhoneNumber),
            cancellationToken);

        var model = new ClientResponseModel(result.Id, result.Name, result.Email, result.PhoneNumber);
        return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
    }

    [HttpGet]
    public async Task<ActionResult<ListResultModel<ClientResponseModel>>> List(CancellationToken cancellationToken)
    {
        var result = await queryBus.QueryAsync(new ListClientsQuery(), cancellationToken);

        var items = result.Items
            .Select(i => new ClientResponseModel(i.Id, i.Name, i.Email, i.PhoneNumber))
            .ToList();

        return new ListResultModel<ClientResponseModel>(items, result.TotalCount);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientResponseModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await queryBus.QueryAsync(new GetClientQuery(id), cancellationToken);

        return result is null
            ? NotFound()
            : new ClientResponseModel(result.Id, result.Name, result.Email, result.PhoneNumber);
    }
}
