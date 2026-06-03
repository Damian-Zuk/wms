using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;
using Wms.Domain.Models;
using Wms.Shared.Common;

namespace Wms.Application.Putaway;

/// <summary>
/// Plans how to split a stock-in line's quantity across one or more locations,
/// filling each to its available capacity. Returns the ordered placements (which
/// always sum to <paramref name="quantity"/>) or a failure when the snapshot can't
/// hold the full quantity.
/// </summary>
public interface IPutawayPlanner
{
    Result<IReadOnlyList<PlacementAllocation>> Plan(
        Product product,
        Lot? lot,
        Quantity quantity,
        PutawayPlanContext context);
}
