using Wms.Domain.Entities;
using Wms.Domain.Enums;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Ranks locations that already hold the same product and same lot.
/// Finite-capacity locations come first and, within those, the one with
/// the LEAST remaining capacity wins — fill-up-first to maximise consolidation.
/// Unlimited locations sort last; address ascending breaks ties.
/// </summary>
internal sealed class ConsolidateSameLotAllocationStrategy : IPutawayAllocationStrategy
{
    public PutawayStrategyType Type => PutawayStrategyType.ConsolidateSameLot;

    public IReadOnlyList<Guid> CandidateLocations(Product product, Lot? lot, PutawayPlanContext context)
    {
        if (lot is null) 
            return [];

        var lotId = lot!.Id;

        return context.Locations
            .Where(l => context.ContentsAt(l.Id)
                .Any(i => i.ProductId == product.Id && i.LotId == lotId))
            .OrderBy(l => l.Capacity.MaxUnits.HasValue ? 0 : 1)
            .ThenBy(l => l.Capacity.MaxUnits.HasValue
                ? l.Capacity.MaxUnits.Value - context.ContentsAt(l.Id).Sum(i => i.OnHand.Value)
                : int.MaxValue)
            .ThenBy(l => l.Address.Zone)
            .ThenBy(l => l.Address.Aisle)
            .ThenBy(l => l.Address.Rack)
            .ThenBy(l => l.Address.Shelf)
            .ThenBy(l => l.Address.Bin)
            .Select(l => l.Id)
            .ToList();
    }
}
