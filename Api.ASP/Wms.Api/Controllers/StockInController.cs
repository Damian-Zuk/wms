using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Handlers.StockIns.Commands;
using Wms.Application.Handlers.StockIns.Queries;
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

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IResult> CreateStockIn(
        [FromBody] CreateStockInCommand request,
        [FromServices] ICommandHandler<CreateStockInCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPatch("{id:guid}/description")]
    public async Task<IResult> UpdateDescription(
        [FromRoute] Guid id,
        [FromBody] UpdateStockInDescriptionRequest request,
        [FromServices] ICommandHandler<UpdateStockInDescriptionCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateStockInDescriptionCommand(id, request.Description),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id:guid}/lines/{lineId:guid}/placements")]
    public async Task<IResult> ModifyLinePlacements(
        [FromRoute] Guid id,
        [FromRoute] Guid lineId,
        [FromBody] ModifyStockInLinePlacementsRequest request,
        [FromServices] ICommandHandler<ModifyStockInLinePlacementsCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ModifyStockInLinePlacementsCommand(id, lineId, request.Placements),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:guid}/lines/{lineId:guid}/replan")]
    public async Task<IResult> ReplanLine(
        [FromRoute] Guid id,
        [FromRoute] Guid lineId,
        [FromServices] ICommandHandler<ReplanStockInLineCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ReplanStockInLineCommand(id, lineId),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/start-putaway")]
    public async Task<IResult> StartPutaway(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<StartPutawayStockInCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new StartPutawayStockInCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/items/{itemId:guid}/putaway")]
    public async Task<IResult> PutawayItem(
        [FromRoute] Guid id,
        [FromRoute] Guid itemId,
        [FromBody] PutawayStockInItemRequest request,
        [FromServices] ICommandHandler<PutawayStockInItemCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new PutawayStockInItemCommand(id, itemId, request.Quantity),
            cancellationToken);

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

    [Authorize(Roles = "Admin,Manager")]
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
