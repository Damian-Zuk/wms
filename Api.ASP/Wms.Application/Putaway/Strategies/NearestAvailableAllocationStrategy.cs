using Wms.Domain.Entities;
using Wms.Domain.Enums;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// The fallback after <see cref="NearestEmptyAllocationStrategy"/>: ranks Storage
/// locations whose temperature zone matches the product and that still have room —
/// finite locations with remaining headroom (typically partly filled), followed by
/// unlimited-capacity locations. Finite locations sort first so bounded space is
/// consumed before falling back to the catch-all unlimited bins; within each group
/// candidates are ordered by ascending address (Zone → Aisle → Rack → Shelf → Bin).
/// </summary>
internal sealed class NearestAvailableAllocationStrategy : IPutawayAllocationStrategy
{
    public PutawayStrategyType Type => PutawayStrategyType.NearestAvailable;

    public IReadOnlyList<Guid> CandidateLocations(Product product, Lot? lot, PutawayPlanContext context)
        => context.Locations
            .Where(l =>
                l.Type == LocationType.Storage &&
                l.IsActive &&
                !l.IsBlocked &&
                l.TemperatureZone == product.RequiredTemperatureZone &&
                HasRoom(context, l))
            .OrderBy(l => l.Capacity.MaxUnits.HasValue ? 0 : 1)
            .ThenBy(l => l.Address.Zone)
            .ThenBy(l => l.Address.Aisle)
            .ThenBy(l => l.Address.Rack)
            .ThenBy(l => l.Address.Shelf)
            .ThenBy(l => l.Address.Bin)
            .Select(l => l.Id)
            .ToList();

    private static bool HasRoom(PutawayPlanContext context, Location location) =>
        // Unlimited always has room; a finite location only if something still fits.
        !location.Capacity.MaxUnits.HasValue
        || location.Capacity.MaxUnits.Value - context.OccupiedUnits(location.Id) > 0;
}
