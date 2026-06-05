using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Dashboard.Queries;

public sealed record StockInStatusCountsDto(int Draft, int Putaway, int Completed, int Cancelled);

/// <summary>Putaway progress across stock-ins currently in the Putaway phase.</summary>
public sealed record PutawayProgressDto(int PlannedUnits, int PlacedUnits, int CompletionPercent);

public sealed record InboundOverviewDto(
    StockInStatusCountsDto StatusCounts,
    PutawayProgressDto PutawayProgress,
    int ManualOverridePercent,
    double? AvgReceivingHours,
    IReadOnlyList<StrategySliceDto> PutawayStrategyMix,
    IReadOnlyList<DailyUnitsDto> ReceivedSeries);

/// <summary>Receiving/putaway metrics for the dashboard Inbound tab over a trailing window.</summary>
public sealed record GetInboundOverviewQuery(int Days = 14) : IQuery<InboundOverviewDto>;

public sealed class GetInboundOverviewQueryHandler(IAppDbContext context)
    : IQueryHandler<GetInboundOverviewQuery, InboundOverviewDto>
{
    public async Task<Result<InboundOverviewDto>> Handle(
        GetInboundOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var days = Math.Clamp(query.Days, 1, 90);
        var fromUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(days - 1)), DateTimeKind.Utc);

        // --- Status counts ---
        var status = await context.StockIns
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Draft = g.Count(s => s.Status == StockInStatus.Draft),
                Putaway = g.Count(s => s.Status == StockInStatus.Putaway),
                Completed = g.Count(s => s.Status == StockInStatus.Completed),
                Cancelled = g.Count(s => s.Status == StockInStatus.Cancelled)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // --- Putaway progress (items of stock-ins still in the Putaway phase) ---
        var progress = await context.StockIns
            .AsNoTracking()
            .Where(s => s.Status == StockInStatus.Putaway)
            .SelectMany(s => s.Lines)
            .SelectMany(l => l.Items)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Planned = g.Sum(i => i.Quantity.Value),
                Placed = g.Sum(i => i.PlacedQuantity.Value)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var planned = progress?.Planned ?? 0;
        var placed = progress?.Placed ?? 0;
        var completionPercent = planned > 0 ? (int)Math.Round(placed * 100.0 / planned) : 0;

        // --- Putaway strategy mix (per placement) + manual-override rate ---
        var strategyRaw = await context.StockInItems
            .AsNoTracking()
            .GroupBy(i => i.Strategy)
            .Select(g => new { Strategy = g.Key, Units = g.Sum(i => i.Quantity.Value) })
            .ToListAsync(cancellationToken);

        var totalUnits = strategyRaw.Sum(x => x.Units);
        var manualUnits = strategyRaw
            .Where(x => x.Strategy == PutawayStrategyType.Manual)
            .Sum(x => x.Units);
        var manualOverridePercent = totalUnits > 0 ? (int)Math.Round(manualUnits * 100.0 / totalUnits) : 0;

        var strategyMix = strategyRaw
            .OrderByDescending(x => x.Units)
            .Select(x => new StrategySliceDto(x.Strategy.ToString(), x.Units))
            .ToList();

        // --- Average receiving cycle time (completed within the window) ---
        var completedTimes = await context.StockIns
            .AsNoTracking()
            .Where(s => s.Status == StockInStatus.Completed && s.UpdatedAt >= fromUtc)
            .Select(s => new { s.CreatedAt, s.UpdatedAt })
            .ToListAsync(cancellationToken);

        double? avgReceivingHours = completedTimes.Count > 0
            ? completedTimes.Average(x => (x.UpdatedAt - x.CreatedAt).TotalHours)
            : null;

        // --- Received units per day (Source = StockIn) ---
        var receivedRaw = await context.StockMovements
            .AsNoTracking()
            .Where(m => m.CreatedAt >= fromUtc && m.Source == StockMovementSource.StockIn)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Units = g.Sum(x => x.QuantityChange) })
            .ToListAsync(cancellationToken);

        var receivedByDay = receivedRaw.ToDictionary(r => DateOnly.FromDateTime(r.Day), r => r.Units);
        var startDate = DateOnly.FromDateTime(fromUtc);
        var receivedSeries = new List<DailyUnitsDto>(days);
        for (var i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            receivedSeries.Add(new DailyUnitsDto(date, receivedByDay.GetValueOrDefault(date)));
        }

        var dto = new InboundOverviewDto(
            new StockInStatusCountsDto(
                status?.Draft ?? 0,
                status?.Putaway ?? 0,
                status?.Completed ?? 0,
                status?.Cancelled ?? 0),
            new PutawayProgressDto(planned, placed, completionPercent),
            manualOverridePercent,
            avgReceivingHours,
            strategyMix,
            receivedSeries);

        return dto;
    }
}
