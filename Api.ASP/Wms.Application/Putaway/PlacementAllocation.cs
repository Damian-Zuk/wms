using Wms.Domain.Enums;

namespace Wms.Application.Putaway;

/// <summary>
/// One planned placement: how much of a stock-in line to put into a location,
/// and which strategy chose it. A line's allocations sum to the line's total.
/// </summary>
public sealed record PlacementAllocation(Guid LocationId, int Quantity, PutawayStrategyType Strategy);
