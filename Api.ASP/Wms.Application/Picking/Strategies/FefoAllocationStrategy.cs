using Wms.Domain.Enums;

namespace Wms.Application.Picking.Strategies;

/// <summary>
/// First-Expired-First-Out. Ranks sources by their lot's expiration date (earliest
/// first; lots without an expiry — and lotless stock — sort last), then by lot number,
/// then by location address for a deterministic, address-ascending walk.
/// </summary>
internal sealed class FefoAllocationStrategy : IPickingAllocationStrategy
{
    public PickingStrategyType Type => PickingStrategyType.Fefo;

    public IReadOnlyList<PickCandidate> RankCandidates(Guid productId, PickingContext context) =>
        context.AvailableFor(productId)
            .OrderBy(c => context.GetLot(c.LotId)?.ExpirationDate is null)
            .ThenBy(c => context.GetLot(c.LotId)?.ExpirationDate)
            .ThenBy(c => context.GetLot(c.LotId)?.Number.Value, StringComparer.Ordinal)
            .ThenBy(c => context.GetLocation(c.LocationId)?.Address)
            .ThenBy(c => c.HandlingUnitId)
            .ToList();
}
