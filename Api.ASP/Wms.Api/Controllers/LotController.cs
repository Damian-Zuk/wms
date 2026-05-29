using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.Lots.Commands;
using Wms.Application.Features.Lots.Queries;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/lots")]
public class LotController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListLots(
        [FromQuery] Guid? productId,
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListLotsQuery, PagedResult<LotDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListLotsQuery(
                productId,
                search,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetLot(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetLotQuery, LotDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetLotQuery(id), cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IResult> CreateLot(
        [FromBody] CreateLotCommand request,
        [FromServices] ICommandHandler<CreateLotCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id:guid}")]
    public async Task<IResult> UpdateLot(
        [FromRoute] Guid id,
        [FromBody] UpdateLotRequest request,
        [FromServices] ICommandHandler<UpdateLotCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateLotCommand(id, request.ManufactureDate, request.ExpirationDate),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpDelete("{id:guid}")]
    public async Task<IResult> DeleteLot(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<DeleteLotCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteLotCommand(id), cancellationToken);
        
        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}

public sealed record UpdateLotRequest(DateOnly? ManufactureDate, DateOnly? ExpirationDate);
