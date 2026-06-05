namespace Wms.Application.Handlers.Dashboard.Queries;

/// <summary>A slice of a strategy-usage breakdown. <see cref="Strategy"/> is the enum name.</summary>
public sealed record StrategySliceDto(string Strategy, int Units);

/// <summary>One day of a single-metric daily series. <see cref="Date"/> serializes as "yyyy-MM-dd".</summary>
public sealed record DailyUnitsDto(DateOnly Date, int Units);
