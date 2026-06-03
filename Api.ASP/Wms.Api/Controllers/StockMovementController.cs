using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Handlers.StockMovements.Queries;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/stock-movements")]
public class StockMovementController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListStockMovements(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? lotId,
        [FromQuery] StockMovementType? type,
        [FromQuery] StockMovementSource? source,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListStockMovementsQuery, PagedResult<StockMovementDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListStockMovementsQuery(
                productId,
                locationId,
                lotId,
                type,
                source,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem); ;
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetStockMovement(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetStockMovementQuery, StockMovementDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetStockMovementQuery(id), cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem); ;
    }
}
