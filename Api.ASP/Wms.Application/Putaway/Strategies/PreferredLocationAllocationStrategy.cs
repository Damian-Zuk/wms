using Wms.Domain.Entities;
using Wms.Domain.Enums;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Ranks the product's preferred locations in their configured sequence order.
/// (Requires the product to have been loaded with its preferred locations.)
/// </summary>
internal sealed class PreferredLocationAllocationStrategy : IPutawayAllocationStrategy
{
    public PutawayStrategyType Type => PutawayStrategyType.PreferredLocation;

    public IReadOnlyList<Guid> CandidateLocations(Product product, Lot? lot, PutawayPlanContext context)
        => product.PreferredLocations
            .OrderBy(pl => pl.Sequence)
            .Select(pl => pl.LocationId)
            .ToList();
}
