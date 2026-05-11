using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.Inventories.Queries;
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
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListInventoriesQuery, PagedResult<InventoryDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListInventoriesQuery(
                productId,
                locationId,
                lotId,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
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
}
