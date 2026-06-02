using Wms.Domain.Entities;
using Wms.Domain.Enums;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Ranks empty Storage locations whose temperature zone matches the product, by
/// ascending address (Zone → Aisle → Rack → Shelf → Bin). Empty means no on-hand
/// inventory rows in the snapshot; the planner still accounts for any active
/// reservations on the location via the shared occupancy.
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
                context.ContentsAt(l.Id).Count == 0)
            .OrderBy(l => l.Address.Zone)
            .ThenBy(l => l.Address.Aisle)
            .ThenBy(l => l.Address.Rack)
            .ThenBy(l => l.Address.Shelf)
            .ThenBy(l => l.Address.Bin)
            .Select(l => l.Id)
            .ToList();
}
