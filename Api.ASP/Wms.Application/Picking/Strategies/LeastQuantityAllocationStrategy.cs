using Wms.Domain.Enums;

namespace Wms.Application.Picking.Strategies;

/// <summary>
/// Least Quantity. Ranks sources by the number of units available (fewest first), so the
/// planner drains the smallest holdings first and clears out fragmented stock. Ties break
/// by lot number, then location address for a deterministic walk.
/// </summary>
internal sealed class LeastQuantityAllocationStrategy : IPickingAllocationStrategy
{
    public PickingStrategyType Type => PickingStrategyType.LeastQuantity;

    public IReadOnlyList<PickCandidate> RankCandidates(Guid productId, PickingContext context) =>
        context.AvailableFor(productId)
            .OrderBy(c => c.Available)
            .ThenBy(c => context.GetLot(c.LotId)?.Number.Value, StringComparer.Ordinal)
            .ThenBy(c => context.GetLocation(c.LocationId)?.Address)
            .ToList();
}
