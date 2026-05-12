using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.StockIns.Commands;
using Wms.Application.Features.StockIns.Queries;
using Wms.Shared.Common;

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

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetStockIn(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetStockInQuery, StockInDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetStockInQuery(id), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost]
    public async Task<IResult> CreateStockIn(
        [FromBody] CreateStockInCommand request,
        [FromServices] ICommandHandler<CreateStockInCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/start-receiving")]
    public async Task<IResult> StartReceiving(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<StartReceivingStockInCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new StartReceivingStockInCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IResult> Receive(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<ReceiveStockInCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ReceiveStockInCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IResult> Complete(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<CompleteStockInCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CompleteStockInCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IResult> Cancel(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<CancelStockInCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CancelStockInCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
