using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wms.Api.Infrastructure;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Handlers.Locations.Commands;
using Wms.Application.Handlers.Locations.Queries;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationController : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListLocations(
        [FromQuery] string? search,
        [FromQuery] string? zone,
        [FromQuery] LocationType? type,
        [FromQuery] TemperatureZone? temperatureZone,
        [FromQuery] string? status,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDescending,
        [FromQuery, Range(0, int.MaxValue)] int page,
        [FromQuery, Range(0, 200)] int pageSize,
        [FromServices] IQueryHandler<ListLocationsQuery, PagedResult<LocationDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ListLocationsQuery(
                search,
                zone,
                type,
                sortBy,
                sortDescending,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize,
                temperatureZone,
                status),
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

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IResult> CreateLocation(
        [FromBody] CreateLocationCommand request,
        [FromServices] ICommandHandler<CreateLocationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(request, cancellationToken);
        
        return result.Match(Results.Ok, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id:guid}")]
    public async Task<IResult> UpdateLocation(
        [FromRoute] Guid id,
        [FromBody] UpdateLocationRequest request,
        [FromServices] ICommandHandler<UpdateLocationCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateLocationCommand(
                id,
                request.Code,
                request.Zone,
                request.Aisle,
                request.Rack,
                request.Shelf,
                request.Bin,
                request.Type,
                request.Description,
                request.TemperatureZone,
                request.Capacity,
                request.WeightCapacity,
                request.VolumeCapacity,
                request.IsMixedSkuAllowed,
                request.IsMixedLotAllowed),
            cancellationToken);
        
        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id:guid}/preferred-products")]
    public async Task<IResult> SetPreferredProducts(
        [FromRoute] Guid id,
        [FromBody] SetLocationPreferredProductsRequest request,
        [FromServices] ICommandHandler<SetLocationPreferredProductsCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new SetLocationPreferredProductsCommand(id, request.ProductIds ?? []),
            cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
    }

    [Authorize(Roles = "Admin,Manager")]
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

public sealed record UpdateLocationRequest(
    string Code,
    string Zone,
    string Aisle,
    string Rack,
    string Shelf,
    string Bin,
    LocationType Type,
    string? Description,
    TemperatureZone TemperatureZone,
    int? Capacity,
    decimal? WeightCapacity,
    decimal? VolumeCapacity,
    bool IsMixedSkuAllowed,
    bool IsMixedLotAllowed);

public sealed record SetLocationPreferredProductsRequest(IReadOnlyList<Guid>? ProductIds);
