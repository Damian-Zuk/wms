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
    Fifo = 2
}
