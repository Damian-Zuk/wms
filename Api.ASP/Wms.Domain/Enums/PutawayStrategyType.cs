namespace Wms.Domain.Enums;

/// <summary>
/// Identifies which putaway strategy produced a stock-in placement, or
/// <see cref="Manual"/> when a user overrode the suggestion.
/// </summary>
public enum PutawayStrategyType
{
    PreferredLocation = 1,
    ConsolidateSameLot = 2,
    ConsolidateSameSku = 3,
    Proximity = 4,
    NearestEmpty = 5,
    NearestAvailable = 6,
    Manual = 7
}
