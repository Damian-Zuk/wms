using Wms.Domain.Entities;
using Wms.Domain.Enums;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Ranks completely empty, finite-capacity Storage locations whose temperature zone
/// matches the product, by ascending address (Zone → Aisle → Rack → Shelf → Bin).
/// Empty means zero occupancy: no on-hand inventory AND no capacity reserved by other
/// stock-ins or committed by sibling placements earlier in the draft. Unlimited
/// locations are deliberately excluded here — <see cref="NearestAvailableAllocationStrategy"/>
/// picks those up next, after every finite location with room.
/// </summary>
internal sealed class NearestEmptyAllocationStrategy : IPutawayAllocationStrategy
{
    public PutawayStrategyType Type => PutawayStrategyType.NearestEmpty;

    public IReadOnlyList<Guid> CandidateLocations(Product product, Lot? lot, PutawayPlanContext context)
        => context.Locations
            .Where(l =>
                l.Type == LocationType.Storage &&
                l.IsActive &&
                !l.IsBlocked &&
                l.TemperatureZone == product.RequiredTemperatureZone &&
                l.Capacity.MaxUnits.HasValue &&
                context.OccupiedUnits(l.Id) == 0)
            .OrderBy(l => l.Address.Zone)
            .ThenBy(l => l.Address.Aisle)
            .ThenBy(l => l.Address.Rack)
            .ThenBy(l => l.Address.Shelf)
            .ThenBy(l => l.Address.Bin)
            .Select(l => l.Id)
            .ToList();
}
