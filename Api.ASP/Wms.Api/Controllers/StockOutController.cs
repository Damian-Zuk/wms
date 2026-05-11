using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.StockOuts.Commands;
using Wms.Application.Features.StockOuts.Queries;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/stock-outs")]
public class StockOutController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListStockOuts(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListStockOutsQuery, PagedResult<StockOutDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListStockOutsQuery(
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetStockOut(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetStockOutQuery, StockOutDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetStockOutQuery(id), cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost]
    public async Task<IResult> CreateStockOut(
        [FromBody] CreateStockOutCommand request,
        [FromServices] ICommandHandler<CreateStockOutCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
