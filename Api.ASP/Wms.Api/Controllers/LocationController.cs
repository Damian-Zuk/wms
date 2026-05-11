using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.Locations.Commands;
using Wms.Application.Features.Locations.Queries;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListLocations(
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListLocationsQuery, PagedResult<LocationDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListLocationsQuery(
                search,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetLocation(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetLocationQuery, LocationDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetLocationQuery(id), cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost]
    public async Task<IResult> CreateLocation(
        [FromBody] CreateLocationCommand request,
        [FromServices] ICommandHandler<CreateLocationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [HttpPost("{id:guid}")]
    public async Task<IResult> UpdateLocation(
        [FromRoute] Guid id,
        [FromBody] UpdateLocationRequest request,
        [FromServices] ICommandHandler<UpdateLocationCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateLocationCommand(id, request.Code, request.Description),
            cancellationToken);
        
        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IResult> DeleteLocation(
        [FromRoute] Guid id,
        [FromServices] ICommandHandler<DeleteLocationCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteLocationCommand(id), cancellationToken);
        
        return result.Match(Results.NoContent, CustomResults.Problem);
    }
}

public sealed record UpdateLocationRequest(string Code, string Description);
