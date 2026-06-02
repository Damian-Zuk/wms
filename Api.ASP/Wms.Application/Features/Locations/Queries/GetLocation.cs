using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Locations.Queries;

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
