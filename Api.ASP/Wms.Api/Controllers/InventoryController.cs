using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Handlers.Inventories.Commands;
using Wms.Application.Handlers.Inventories.Queries;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/inventories")]
public class InventoryController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListInventories(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? lotId,
        [FromQuery] int? expiringWithinDays,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDescending,
        [FromQuery, Range(0, int.MaxValue)] int page,
        [FromQuery, Range(0, 100)] int pageSize,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? handlingUnitId,
        [FromServices] IQueryHandler<ListInventoriesQuery, PagedResult<InventoryDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListInventoriesQuery(
                productId,
                locationId,
                lotId,
                expiringWithinDays,
                sortBy,
                sortDescending,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize,
                categoryId,
                handlingUnitId),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetInventory(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetInventoryQuery, InventoryDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetInventoryQuery(id), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:guid}/adjust")]
    public async Task<IResult> AdjustInventory(
        [FromRoute] Guid id,
        [FromBody] AdjustInventoryRequest request,
        [FromServices] ICommandHandler<AdjustInventoryCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new AdjustInventoryCommand(id, request.QuantityChange, request.Reason),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpGet("availability")]
    public async Task<IResult> GetAvailability(
        [FromQuery] Guid productId,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? lotId,
        [FromQuery] Guid? handlingUnitId,
        [FromServices] IQueryHandler<GetAvailabilityQuery, AvailabilityDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new GetAvailabilityQuery(productId, locationId, lotId, handlingUnitId),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}

public sealed record AdjustInventoryRequest(int QuantityChange, string? Reason);
