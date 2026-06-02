namespace Wms.Domain.Enums;

/// <summary>
/// Identifies which putaway strategy produced a stock-in placement, or
/// <see cref="Manual"/> when a user overrode the suggestion.
/// </summary>
public enum PutawayStrategyType
{
    FixedLocation = 1,
    ConsolidateSameSku = 2,
    NearestEmpty = 3,
    Manual = 4
}
