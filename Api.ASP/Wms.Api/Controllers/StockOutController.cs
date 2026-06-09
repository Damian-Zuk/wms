using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Handlers.StockOuts.Commands;
using Wms.Application.Handlers.StockOuts.Queries;
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

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IResult> CreateStockOut(
        [FromBody] CreateStockOutCommand request,
        [FromServices] ICommandHandler<CreateStockOutCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id:guid}/lines/{lineId:guid}/pick-locations")]
    public async Task<IResult> ModifyPickLocations(
        [FromRoute] Guid id,
        [FromRoute] Guid lineId,
        [FromBody] ModifyPickLocationsRequest request,
        [FromServices] ICommandHandler<ModifyPickLocationsCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ModifyPickLocationsCommand(id, lineId, request.Allocations),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:guid}/lines/{lineId:guid}/replan")]
    public async Task<IResult> ReplanLine(
        [FromRoute] Guid id,
        [FromRoute] Guid lineId,
        [FromBody] ReplanStockOutLineRequest request,
        [FromServices] ICommandHandler<ReplanStockOutLineCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ReplanStockOutLineCommand(id, lineId, request.Strategy),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/start-picking")]
    public async Task<IResult> StartPicking(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<StartPickingStockOutCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new StartPickingStockOutCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/items/{itemId:guid}/pick")]
    public async Task<IResult> PickItem(
        [FromRoute] Guid id,
        [FromRoute] Guid itemId,
        [FromBody] PickStockOutItemRequest request,
        [FromServices] ICommandHandler<PickStockOutItemCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new PickStockOutItemCommand(id, itemId, request.Quantity),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IResult> Complete(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<CompleteStockOutCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CompleteStockOutCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IResult> Cancel(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<CancelStockOutCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CancelStockOutCommand(id), cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}
