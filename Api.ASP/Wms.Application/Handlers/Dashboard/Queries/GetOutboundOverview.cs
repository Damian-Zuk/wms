using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Dashboard.Queries;

public sealed record StockOutStatusCountsDto(int Draft, int Picking, int Completed, int Cancelled);

/// <summary>Pick progress across stock-outs currently in the Picking phase.</summary>
public sealed record PickProgressDto(int PlannedUnits, int PickedUnits, int CompletionPercent);

/// <summary>How cancelled stock-outs were split by the phase they were cancelled from.</summary>
public sealed record CancellationsDto(int FromDraft, int FromPicking);

public sealed record TopProductDto(Guid ProductId, string Sku, string Name, int Units);

public sealed record OutboundOverviewDto(
    StockOutStatusCountsDto StatusCounts,
    PickProgressDto PickProgress,
    double? AvgFulfillmentHours,
    CancellationsDto Cancellations,
    IReadOnlyList<StrategySliceDto> PickingStrategyMix,
    IReadOnlyList<TopProductDto> TopPickedProducts,
    IReadOnlyList<DailyUnitsDto> ShippedSeries);

/// <summary>Picking/fulfillment metrics for the dashboard Outbound tab over a trailing window.</summary>
public sealed record GetOutboundOverviewQuery(int Days = 14) : IQuery<OutboundOverviewDto>;

public sealed class GetOutboundOverviewQueryHandler(IAppDbContext context)
    : IQueryHandler<GetOutboundOverviewQuery, OutboundOverviewDto>
{
    public async Task<Result<OutboundOverviewDto>> Handle(
        GetOutboundOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var days = Math.Clamp(query.Days, 1, 90);
        var fromUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(days - 1)), DateTimeKind.Utc);

        // --- Status counts ---
        var status = await context.StockOuts
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Draft = g.Count(s => s.Status == StockOutStatus.Draft),
                Picking = g.Count(s => s.Status == StockOutStatus.Picking),
                Completed = g.Count(s => s.Status == StockOutStatus.Completed),
                Cancelled = g.Count(s => s.Status == StockOutStatus.Cancelled)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // --- Pick progress (items of stock-outs still in the Picking phase) ---
        var progress = await context.StockOuts
            .AsNoTracking()
            .Where(s => s.Status == StockOutStatus.Picking)
            .SelectMany(s => s.Lines)
            .SelectMany(l => l.Items)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Planned = g.Sum(i => i.Quantity.Value),
                Picked = g.Sum(i => i.PickedQuantity.Value)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var planned = progress?.Planned ?? 0;
        var picked = progress?.Picked ?? 0;
        var completionPercent = planned > 0 ? (int)Math.Round(picked * 100.0 / planned) : 0;

        // --- Cancellations split by the phase they were cancelled from ---
        var cancellations = await context.StockOuts
            .AsNoTracking()
            .Where(s => s.Status == StockOutStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                FromDraft = g.Count(s => s.CancelledFrom == StockOutStatus.Draft),
                FromPicking = g.Count(s => s.CancelledFrom == StockOutStatus.Picking)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // --- Picking strategy mix (per line) ---
        var strategyRaw = await context.StockOutLines
            .AsNoTracking()
            .GroupBy(l => l.Strategy)
            .Select(g => new { Strategy = g.Key, Units = g.Sum(l => l.Quantity.Value) })
            .ToListAsync(cancellationToken);

        var strategyMix = strategyRaw
            .OrderByDescending(x => x.Units)
            .Select(x => new StrategySliceDto(x.Strategy.ToString(), x.Units))
            .ToList();

        // --- Average fulfillment cycle time (completed within the window) ---
        var completedTimes = await context.StockOuts
            .AsNoTracking()
            .Where(s => s.Status == StockOutStatus.Completed && s.UpdatedAt >= fromUtc)
            .Select(s => new { s.CreatedAt, s.UpdatedAt })
            .ToListAsync(cancellationToken);

        double? avgFulfillmentHours = completedTimes.Count > 0
            ? completedTimes.Average(x => (x.UpdatedAt - x.CreatedAt).TotalHours)
            : null;

        // --- Top picked products in the window (Source = StockOut) ---
        var topRaw = await context.StockMovements
            .AsNoTracking()
            .Where(m => m.Source == StockMovementSource.StockOut && m.CreatedAt >= fromUtc)
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.QuantityChange) })
            .OrderByDescending(x => x.Units)
            .Take(5)
            .ToListAsync(cancellationToken);

        var products = await RefLookup.LoadProductRefsAsync(
            context,
            topRaw.Select(t => t.ProductId).ToList(),
            cancellationToken);

        var topProducts = topRaw
            .Select(t =>
            {
                var p = products.GetValueOrDefault(t.ProductId);
                return new TopProductDto(t.ProductId, p?.Sku ?? "—", p?.Name ?? "Unknown", t.Units);
            })
            .ToList();

        // --- Shipped units per day (Source = StockOut) ---
        var shippedRaw = await context.StockMovements
            .AsNoTracking()
            .Where(m => m.CreatedAt >= fromUtc && m.Source == StockMovementSource.StockOut)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Units = g.Sum(x => x.QuantityChange) })
            .ToListAsync(cancellationToken);

        var shippedByDay = shippedRaw.ToDictionary(r => DateOnly.FromDateTime(r.Day), r => r.Units);
        var startDate = DateOnly.FromDateTime(fromUtc);
        var shippedSeries = new List<DailyUnitsDto>(days);
        for (var i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            shippedSeries.Add(new DailyUnitsDto(date, shippedByDay.GetValueOrDefault(date)));
        }

        var dto = new OutboundOverviewDto(
            new StockOutStatusCountsDto(
                status?.Draft ?? 0,
                status?.Picking ?? 0,
                status?.Completed ?? 0,
                status?.Cancelled ?? 0),
            new PickProgressDto(planned, picked, completionPercent),
            avgFulfillmentHours,
            new CancellationsDto(
                cancellations?.FromDraft ?? 0,
                cancellations?.FromPicking ?? 0),
            strategyMix,
            topProducts,
            shippedSeries);

        return dto;
    }
}
