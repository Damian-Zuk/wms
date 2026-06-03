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
    NearestEmpty = 4,
    Manual = 5
}
