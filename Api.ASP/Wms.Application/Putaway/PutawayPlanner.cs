using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Putaway;

/// <summary>
/// Walks the allocation strategies in registration order. For each candidate
/// location it gates with <see cref="Location.CanAccept(Product, Lot?, Quantity, IEnumerable{Inventory}, Services.CapacityOccupancy)"/>,
/// then takes as much of the remaining quantity as fits (the whole remainder for
/// unlimited locations), committing the load back into the shared context so later
/// candidates and later lines see the space as used. Fails if anything is left
/// unplaced after all strategies.
/// </summary>
internal sealed class PutawayPlanner(IEnumerable<IPutawayAllocationStrategy> strategies) : IPutawayPlanner
{
    public Result<IReadOnlyList<PlacementAllocation>> Plan(
        Product product,
        Lot? lot,
        Quantity quantity,
        PutawayPlanContext context)
    {
        var remaining = quantity.Value;
        var allocations = new List<PlacementAllocation>();

        foreach (var strategy in strategies)
        {
            if (remaining == 0)
                break;

            foreach (var locationId in strategy.CandidateLocations(product, lot, context))
            {
                if (remaining == 0)
                    break;

                var location = context.GetLocation(locationId);
                if (location is null)
                    continue;

                var contents = context.ContentsAt(locationId);
                var occupancy = context.OccupancyFor(locationId);

                // Gate zone / mixed-sku / mixed-lot / blocked / capacity-for-one before sizing.
                if (location.CanAccept(product, lot, new Quantity(1), contents, occupancy).IsFailure)
                    continue;

                // CanAccept only sees committed inventory; also reject if a different
                // SKU/lot was already planned into this bin earlier in the same draft.
                if (context.WouldConflictWithPlanned(location, product, lot))
                    continue;

                var headroom = location.UnitsThatFit(contents, occupancy);
                var take = headroom is null ? remaining : Math.Min(remaining, headroom.Value);
                if (take <= 0)
                    continue;

                AddOrMerge(allocations, locationId, take, strategy.Type);
                context.Commit(locationId, product, lot, new Quantity(take));
                remaining -= take;
            }
        }

        if (remaining > 0)
            return PutawayErrors.CannotPlaceFullQuantity(product.Id, lot?.Id, quantity.Value, remaining);

        return Result.Success<IReadOnlyList<PlacementAllocation>>(allocations);
    }

    private static void AddOrMerge(
        List<PlacementAllocation> allocations,
        Guid locationId,
        int quantity,
        PutawayStrategyType strategy)
    {
        var index = allocations.FindIndex(a => a.LocationId == locationId);
        if (index >= 0)
            allocations[index] = allocations[index] with { Quantity = allocations[index].Quantity + quantity };
        else
            allocations.Add(new PlacementAllocation(locationId, quantity, strategy));
    }
}
