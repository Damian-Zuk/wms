using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Dashboard.Queries;

public sealed record InventorySummaryDto(
    int OnHandUnits,
    int ReservedUnits,
    int AvailableUnits,
    int DistinctSkus,
    int OccupiedLocations,
    int ActiveLocations);

public sealed record FlowTodayDto(
    int ReceivedToday,
    int ShippedToday,
    int ReceivedPrevDay,
    int ShippedPrevDay);

public sealed record WorkOrdersDto(
    int DraftStockIns,
    int PutawayStockIns,
    int DraftStockOuts,
    int PickingStockOuts);

public sealed record DashboardAlertsDto(
    int ExpiringSoonLots,
    int ExpiredOnHandLots,
    int BlockedLocations,
    int InactiveLocations);

public sealed record ThroughputPointDto(DateOnly Date, int Received, int Shipped);

public sealed record DashboardOverviewDto(
    InventorySummaryDto Inventory,
    FlowTodayDto FlowToday,
    WorkOrdersDto WorkOrders,
    DashboardAlertsDto Alerts,
    IReadOnlyList<ThroughputPointDto> Throughput);

/// <summary>
/// A single round-trip summary for the dashboard Overview tab: current inventory
/// totals, today's receiving/shipping flow (with the prior day for deltas), open
/// work-order counts, operational alerts, and a daily throughput series over the
/// trailing <see cref="Days"/> window.
/// </summary>
public sealed record GetDashboardOverviewQuery(int Days = 14) : IQuery<DashboardOverviewDto>;

public sealed class GetDashboardOverviewQueryHandler(IAppDbContext context)
    : IQueryHandler<GetDashboardOverviewQuery, DashboardOverviewDto>
{
    public async Task<Result<DashboardOverviewDto>> Handle(
        GetDashboardOverviewQuery query,
        CancellationToken cancellationToken)
    {
        // At least 2 days so FlowToday can derive a previous day from the series.
        var days = Math.Clamp(query.Days, 2, 90);

        // --- Inventory totals (single-row aggregate) ---
        var invTotals = await context.Inventories
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OnHand = g.Sum(i => i.OnHand.Value),
                Reserved = g.Sum(i => i.Reserved.Value)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var onHand = invTotals?.OnHand ?? 0;
        var reserved = invTotals?.Reserved ?? 0;

        // Counted over physically-present stock only (OnHand > 0).
        var presentStock = context.Inventories.AsNoTracking().Where(i => i.OnHand.Value > 0);
        var distinctSkus = await presentStock.Select(i => i.ProductId).Distinct().CountAsync(cancellationToken);
        var occupiedLocations = await presentStock.Select(i => i.LocationId).Distinct().CountAsync(cancellationToken);

        // --- Location stats (active / inactive / blocked in one pass) ---
        var locStats = await context.Locations
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Active = g.Count(l => l.IsActive),
                Inactive = g.Count(l => !l.IsActive),
                Blocked = g.Count(l => l.IsBlocked)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // --- Open work orders ---
        var siStats = await context.StockIns
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Draft = g.Count(s => s.Status == StockInStatus.Draft),
                Putaway = g.Count(s => s.Status == StockInStatus.Putaway)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var soStats = await context.StockOuts
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Draft = g.Count(s => s.Status == StockOutStatus.Draft),
                Picking = g.Count(s => s.Status == StockOutStatus.Picking)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // --- Expiry alerts (lots that still have physical stock) ---
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var soon = today.AddDays(30); // mirrors Lot.IsExpiringSoon default window

        var onHandLotIds = context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0 && i.LotId != null)
            .Select(i => i.LotId!.Value);

        var lotAlerts = await context.Lots
            .AsNoTracking()
            .Where(l => onHandLotIds.Contains(l.Id) && l.ExpirationDate != null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ExpiringSoon = g.Count(l => l.ExpirationDate >= today && l.ExpirationDate <= soon),
                Expired = g.Count(l => l.ExpirationDate < today)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // --- Throughput series: receiving (StockIn) vs shipping (StockOut) per day ---
        var fromUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-(days - 1)), DateTimeKind.Utc);

        var raw = await context.StockMovements
            .AsNoTracking()
            .Where(m => m.CreatedAt >= fromUtc
                && (m.Source == StockMovementSource.StockIn || m.Source == StockMovementSource.StockOut))
            .GroupBy(m => new { Day = m.CreatedAt.Date, m.Source })
            .Select(g => new { g.Key.Day, g.Key.Source, Units = g.Sum(x => x.QuantityChange) })
            .ToListAsync(cancellationToken);

        var byDay = raw
            .GroupBy(r => DateOnly.FromDateTime(r.Day))
            .ToDictionary(
                g => g.Key,
                g => (
                    Received: g.Where(x => x.Source == StockMovementSource.StockIn).Sum(x => x.Units),
                    Shipped: g.Where(x => x.Source == StockMovementSource.StockOut).Sum(x => x.Units)));

        var startDate = DateOnly.FromDateTime(fromUtc);
        var throughput = new List<ThroughputPointDto>(days);
        for (var i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var point = byDay.TryGetValue(date, out var v) ? v : (Received: 0, Shipped: 0);
            throughput.Add(new ThroughputPointDto(date, point.Received, point.Shipped));
        }

        // FlowToday is the last point (today) vs the one before it (yesterday).
        var todayPoint = throughput[^1];
        var prevPoint = throughput[^2];

        var dto = new DashboardOverviewDto(
            new InventorySummaryDto(
                onHand,
                reserved,
                onHand - reserved,
                distinctSkus,
                occupiedLocations,
                locStats?.Active ?? 0),
            new FlowTodayDto(
                todayPoint.Received,
                todayPoint.Shipped,
                prevPoint.Received,
                prevPoint.Shipped),
            new WorkOrdersDto(
                siStats?.Draft ?? 0,
                siStats?.Putaway ?? 0,
                soStats?.Draft ?? 0,
                soStats?.Picking ?? 0),
            new DashboardAlertsDto(
                lotAlerts?.ExpiringSoon ?? 0,
                lotAlerts?.Expired ?? 0,
                locStats?.Blocked ?? 0,
                locStats?.Inactive ?? 0),
            throughput);

        return dto;
    }
}
