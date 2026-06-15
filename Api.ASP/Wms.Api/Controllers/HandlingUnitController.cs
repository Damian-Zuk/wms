using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Handlers.HandlingUnits.Commands;
using Wms.Application.Handlers.HandlingUnits.Queries;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/handling-units")]
public class HandlingUnitController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListHandlingUnits(
        [FromQuery] string? search,
        [FromQuery] Guid? locationId,
        [FromQuery] HandlingUnitType? type,
        [FromQuery] bool? isEmpty,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDescending,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListHandlingUnitsQuery, PagedResult<HandlingUnitListItemDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListHandlingUnitsQuery(
                search,
                locationId,
                type,
                isEmpty,
                sortBy,
                sortDescending,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetHandlingUnit(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetHandlingUnitQuery, HandlingUnitDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetHandlingUnitQuery(id), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IResult> CreateHandlingUnit(
        [FromBody] CreateHandlingUnitCommand request,
        [FromServices] ICommandHandler<CreateHandlingUnitCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:guid}/move")]
    public async Task<IResult> MoveHandlingUnit(
        [FromRoute] Guid id,
        [FromBody] MoveHandlingUnitRequest request,
        [FromServices] ICommandHandler<MoveHandlingUnitCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new MoveHandlingUnitCommand(id, request.DestinationLocationId),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:guid}/pack")]
    public async Task<IResult> PackHandlingUnit(
        [FromRoute] Guid id,
        [FromBody] PackHandlingUnitRequest request,
        [FromServices] ICommandHandler<PackHandlingUnitCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new PackHandlingUnitCommand(id, request.ProductId, request.LotId, request.Quantity),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:guid}/unpack")]
    public async Task<IResult> UnpackHandlingUnit(
        [FromRoute] Guid id,
        [FromBody] UnpackHandlingUnitRequest request,
        [FromServices] ICommandHandler<UnpackHandlingUnitCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UnpackHandlingUnitCommand(id, request.ProductId, request.LotId, request.Quantity),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpDelete("{id:guid}")]
    public async Task<IResult> DeleteHandlingUnit(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<DeleteHandlingUnitCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteHandlingUnitCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
