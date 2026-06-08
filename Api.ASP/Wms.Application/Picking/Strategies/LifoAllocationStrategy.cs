using Wms.Domain.Enums;

namespace Wms.Application.Picking.Strategies;

/// <summary>
/// Last-In-First-Out. The mirror of FIFO: ranks sources by the date stock was received
/// (latest first; sources without a received date sort last), then by lot number, then
/// by location address for a deterministic walk. Works for lotless products too, since
/// the received date lives on the inventory row, not the lot.
/// </summary>
internal sealed class LifoAllocationStrategy : IPickingAllocationStrategy
{
    public PickingStrategyType Type => PickingStrategyType.Lifo;

    public IReadOnlyList<PickCandidate> RankCandidates(Guid productId, PickingContext context) =>
        context.AvailableFor(productId)
            .OrderBy(c => c.ReceivedAt is null)
            .ThenByDescending(c => c.ReceivedAt)
            .ThenBy(c => context.GetLot(c.LotId)?.Number.Value, StringComparer.Ordinal)
            .ThenBy(c => context.GetLocation(c.LocationId)?.Address)
            .ToList();
}
