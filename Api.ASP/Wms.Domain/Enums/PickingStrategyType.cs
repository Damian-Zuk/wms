namespace Wms.Domain.Enums;

/// <summary>
/// Identifies which picking strategy a stock-out line uses to allocate its
/// requested quantity across inventory sources (location + lot).
/// </summary>
public enum PickingStrategyType
{
    /// <summary>First-Expired-First-Out: draw from the earliest-expiring lots first.</summary>
    Fefo = 1,

    /// <summary>First-In-First-Out: draw from the earliest-received stock first.</summary>
    Fifo = 2,

    /// <summary>Last-In-First-Out: draw from the most-recently-received stock first.</summary>
    Lifo = 3,

    /// <summary>Draw from the sources holding the fewest available units first, clearing out small/fragmented stock.</summary>
    LeastQuantity = 4,

    /// <summary>A user hand-picked the location/lot allocations, overriding any strategy suggestion.</summary>
    Manual = 5
}
