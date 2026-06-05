using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Locations.Queries;

public sealed record LocationAddressDto(
    string Zone,
    string Aisle,
    string Rack,
    string Shelf,
    string Bin);

public sealed record LocationDto(
    Guid Id,
    string Code,
    LocationAddressDto Address,
    string Display,
    LocationType Type,
    TemperatureZone TemperatureZone,
    int? Capacity,
    int Occupancy,
    decimal? WeightCapacity,
    decimal WeightOccupancy,
    decimal? VolumeCapacity,
    decimal VolumeOccupancy,
    bool IsMixedSkuAllowed,
    bool IsMixedLotAllowed,
    bool IsActive,
    bool IsBlocked,
    string? BlockedReason,
    string? Description);

public sealed record GetLocationQuery(Guid Id) : IQuery<LocationDto>;

public sealed class GetLocationQueryHandler(IAppDbContext context)
    : IQueryHandler<GetLocationQuery, LocationDto>
{
    public async Task<Result<LocationDto>> Handle(GetLocationQuery query, CancellationToken cancellationToken)
    {
        var location = await context.Locations
            .AsNoTracking()
            .Where(l => l.Id == query.Id)
            .Select(l => new LocationDto(
                l.Id,
                l.Code.Value,
                new LocationAddressDto(
                    l.Address.Zone,
                    l.Address.Aisle,
                    l.Address.Rack,
                    l.Address.Shelf,
                    l.Address.Bin),
                l.Address.ToString(),
                l.Type,
                l.TemperatureZone,
                l.Capacity.MaxUnits,
                context.Inventories
                    .Where(i => i.LocationId == l.Id)
                    .Sum(i => i.OnHand.Value),
                l.Capacity.MaxWeight,
                context.Inventories
                    .Where(i => i.LocationId == l.Id)
                    .Join(context.Products, i => i.ProductId, p => p.Id, (i, p) => i.OnHand.Value * p.Weight)
                    .Sum(),
                l.Capacity.MaxVolume,
                context.Inventories
                    .Where(i => i.LocationId == l.Id)
                    .Join(context.Products, i => i.ProductId, p => p.Id, (i, p) => i.OnHand.Value * p.Volume)
                    .Sum(),
                l.IsMixedSkuAllowed,
                l.IsMixedLotAllowed,
                l.IsActive,
                l.IsBlocked,
                l.BlockedReason,
                l.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return location is null ? LocationErrors.NotFound(query.Id) : location;
    }
}
