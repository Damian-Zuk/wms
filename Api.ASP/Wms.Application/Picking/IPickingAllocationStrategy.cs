using Wms.Domain.Enums;

namespace Wms.Application.Picking;

/// <summary>
/// Ranks the available inventory sources for a product, best-first, according to a
/// picking discipline (FEFO, FIFO, …). stock-out line uses exactly one picking
/// strategy, so the planner runs the single matching strategy and takes greedily 
/// down its ranking. Strategies are pure and read-only: they only read the 
/// pre-loaded <see cref="PickingContext"/> snapshot.
/// </summary>
public interface IPickingAllocationStrategy
{
    PickingStrategyType Type { get; }

    IReadOnlyList<PickCandidate> RankCandidates(Guid productId, PickingContext context);
}
