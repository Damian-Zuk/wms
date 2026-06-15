using Wms.Domain.Enums;

namespace Wms.Domain.Models;

/// <summary>
/// One planned placement: how much of a stock-in line to put into a location,
/// and which strategy chose it. A line's allocations sum to the line's total.
/// <see cref="HandlingUnitId"/> binds the placement to a declared handling unit
/// (which must land whole in one location); null = loose stock.
/// </summary>
public sealed record PlacementAllocation(
    Guid LocationId,
    int Quantity,
    PutawayStrategyType Strategy,
    Guid? HandlingUnitId = null);
