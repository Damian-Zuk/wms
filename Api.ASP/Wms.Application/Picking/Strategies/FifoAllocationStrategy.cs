using Wms.Domain.Enums;

namespace Wms.Application.Picking.Strategies;

/// <summary>
/// First-In-First-Out. Ranks sources by the date stock was received (earliest first;
/// sources without a received date sort last), then by lot number, then by location
/// address for a deterministic, address-ascending walk. Works for lotless products
/// too, since the received date lives on the inventory row, not the lot.
/// </summary>
internal sealed class FifoAllocationStrategy : IPickingAllocationStrategy
{
    public PickingStrategyType Type => PickingStrategyType.Fifo;

    public IReadOnlyList<PickCandidate> RankCandidates(Guid productId, PickingContext context) =>
        context.AvailableFor(productId)
            .OrderBy(c => c.ReceivedAt is null)
            .ThenBy(c => c.ReceivedAt)
            .ThenBy(c => context.GetLot(c.LotId)?.Number.Value, StringComparer.Ordinal)
            .ThenBy(c => context.GetLocation(c.LocationId)?.Address)
            .ThenBy(c => c.HandlingUnitId)
            .ToList();
}
