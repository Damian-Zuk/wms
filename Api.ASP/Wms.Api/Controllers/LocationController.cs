using Microsoft.AspNetCore.Mvc;
using Wms.Api.Extensions;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Features.Locations.Commands;
using Wms.Application.Features.Locations.Queries;

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
        var result = await handler.Handle(new ListLocationsQuery(search, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IResult> GetLocation(
        [FromRoute] Guid id,
        [FromServices] IQueryHandler<GetLocationQuery, LocationDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetLocationQuery(id), cancellationToken);
        return result.ToHttpResult();
    }

    [HttpPost]
    public async Task<IResult> CreateLocation(
        [FromBody] CreateLocationCommand request,
        [FromServices] ICommandHandler<CreateLocationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        return result.ToHttpResult();
    }
}
