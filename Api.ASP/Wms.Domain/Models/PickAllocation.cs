using Wms.Domain.Enums;

namespace Wms.Domain.Models;

/// <summary>
/// One planned pick: how much of a stock-out line to draw from a specific
/// location + lot (+ handling unit, when the stock sits on one), and which strategy
/// chose it. A line's allocations sum to the line's total. <see cref="LotId"/> is
/// null for products that are not lot-tracked; <see cref="HandlingUnitId"/> is null
/// for loose stock.
/// </summary>
public sealed record PickAllocation(
    Guid LocationId,
    Guid? LotId,
    int Quantity,
    PickingStrategyType Strategy,
    Guid? HandlingUnitId = null);
