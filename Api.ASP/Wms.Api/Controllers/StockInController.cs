using Microsoft.AspNetCore.Mvc;
using Wms.Api.Extensions;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.StockIns.Commands;
using Wms.Application.Features.StockIns.Queries;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/stock-ins")]
public class StockInController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListStockIns(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListStockInsQuery, PagedResult<StockInDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListStockInsQuery(
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetStockIn(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetStockInQuery, StockInDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetStockInQuery(id), cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost]
    public async Task<IResult> CreateStockIn(
        [FromBody] CreateStockInCommand request,
        [FromServices] ICommandHandler<CreateStockInCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }
}
