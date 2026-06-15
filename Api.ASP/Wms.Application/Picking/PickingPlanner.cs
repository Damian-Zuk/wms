using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Models;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Picking;

/// <summary>
/// Resolves the one strategy the line asked for, ranks the product's available sources
/// with it, then greedily takes from each in order until the requested quantity is met.
/// Commits each take back into the shared context so sibling lines of the same draft
/// can't draw the same units. Fails if the snapshot can't cover the full quantity.
/// </summary>
internal sealed class PickingPlanner(IEnumerable<IPickingAllocationStrategy> strategies) : IPickingPlanner
{
    public Result<IReadOnlyList<PickAllocation>> Plan(
        Guid productId,
        PickingStrategyType strategy,
        Quantity quantity,
        PickingContext context)
    {
        var picker = strategies.FirstOrDefault(s => s.Type == strategy);
        if (picker is null)
            return PickingErrors.UnknownStrategy(strategy);

        var remaining = quantity.Value;
        var allocations = new List<PickAllocation>();

        foreach (var candidate in picker.RankCandidates(productId, context))
        {
            if (remaining == 0)
                break;

            if (candidate.Available <= 0)
                continue;

            var take = Math.Min(remaining, candidate.Available);
            allocations.Add(new PickAllocation(
                candidate.LocationId, candidate.LotId, take, strategy, candidate.HandlingUnitId));
            context.Commit(productId, candidate.LocationId, candidate.LotId, candidate.HandlingUnitId, take);
            remaining -= take;
        }

        if (remaining > 0)
            return PickingErrors.CannotPickFullQuantity(productId, strategy, quantity.Value, remaining);

        return Result.Success<IReadOnlyList<PickAllocation>>(allocations);
    }
}
