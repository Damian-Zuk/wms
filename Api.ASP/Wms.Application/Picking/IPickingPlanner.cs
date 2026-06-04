using Wms.Domain.Enums;
using Wms.Domain.Models;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Picking;

/// <summary>
/// Plans how to draw a stock-out line's quantity from one or more inventory sources
/// using the line's chosen strategy. Returns the allocations (which always sum to
/// <paramref name="quantity"/>) or a failure when the snapshot can't cover the full
/// quantity.
/// </summary>
public interface IPickingPlanner
{
    Result<IReadOnlyList<PickAllocation>> Plan(
        Guid productId,
        PickingStrategyType strategy,
        Quantity quantity,
        PickingContext context);
}
