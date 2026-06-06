using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Dashboard.Queries;

public sealed record InventoryStockSummaryDto(
    int OnHandUnits,
    int ReservedUnits,
    int AvailableUnits,
    int DistinctSkus,
    decimal TotalWeightKg,
    decimal TotalVolume);

/// <summary>A slice of an on-hand composition breakdown. <see cref="Key"/> is the enum name.</summary>
public sealed record CompositionSliceDto(string Key, int Units);

/// <summary>On-hand units in lot-tracked stock bucketed by days-to-expiry.</summary>
public sealed record ExpiryBucketsDto(
    int Expired,
    int Within7,
    int Within30,
    int Within60,
    int Within90,
    int Beyond90,
    int NoExpiry);

public sealed record InventoryOverviewDto(
    InventoryStockSummaryDto Summary,
    IReadOnlyList<CompositionSliceDto> ByTemperatureZone,
    IReadOnlyList<CompositionSliceDto> ByLocationType,
    IReadOnlyList<TopProductDto> TopProducts,
    ExpiryBucketsDto ExpiryBuckets);

/// <summary>Stock composition + expiry metrics for the dashboard Inventory tab.</summary>
public sealed record GetInventoryOverviewQuery : IQuery<InventoryOverviewDto>;

public sealed class GetInventoryOverviewQueryHandler(IAppDbContext context)
    : IQueryHandler<GetInventoryOverviewQuery, InventoryOverviewDto>
{
    public async Task<Result<InventoryOverviewDto>> Handle(
        GetInventoryOverviewQuery query,
        CancellationToken cancellationToken)
    {
        // --- Totals ---
        var totals = await context.Inventories
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OnHand = g.Sum(i => i.OnHand.Value),
                Reserved = g.Sum(i => i.Reserved.Value)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var onHand = totals?.OnHand ?? 0;
        var reserved = totals?.Reserved ?? 0;

        var distinctSkus = await context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0)
            .Select(i => i.ProductId)
            .Distinct()
            .CountAsync(cancellationToken);

        var loadAgg = await context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0)
            .Join(context.Products, i => i.ProductId, p => p.Id,
                (i, p) => new { Units = i.OnHand.Value, p.Weight, p.Volume })
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Weight = g.Sum(x => x.Units * x.Weight),
                Volume = g.Sum(x => x.Units * x.Volume)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // --- Composition by location temperature zone ---
        var byZone = await context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0)
            .Join(context.Locations, i => i.LocationId, l => l.Id,
                (i, l) => new { l.TemperatureZone, Units = i.OnHand.Value })
            .GroupBy(x => x.TemperatureZone)
            .Select(g => new { g.Key, Units = g.Sum(x => x.Units) })
            .ToListAsync(cancellationToken);

        var byLocationType = await context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0)
            .Join(context.Locations, i => i.LocationId, l => l.Id,
                (i, l) => new { l.Type, Units = i.OnHand.Value })
            .GroupBy(x => x.Type)
            .Select(g => new { g.Key, Units = g.Sum(x => x.Units) })
            .ToListAsync(cancellationToken);

        // --- Top SKUs by on-hand ---
        var topRaw = await context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(i => i.OnHand.Value) })
            .OrderByDescending(x => x.Units)
            .Take(10)
            .ToListAsync(cancellationToken);

        var products = await RefLookup.LoadProductRefsAsync(
            context, topRaw.Select(t => t.ProductId).ToList(), cancellationToken);

        var topProducts = topRaw
            .Select(t =>
            {
                var p = products.GetValueOrDefault(t.ProductId);
                return new TopProductDto(t.ProductId, p?.Sku ?? "—", p?.Name ?? "Unknown", t.Units);
            })
            .ToList();

        // --- Expiry buckets (lot-tracked on-hand stock) ---
        var expRaw = await context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0 && i.LotId != null)
            .Join(context.Lots, i => i.LotId!.Value, l => l.Id,
                (i, l) => new { Units = i.OnHand.Value, l.ExpirationDate })
            .GroupBy(x => x.ExpirationDate)
            .Select(g => new { ExpirationDate = g.Key, Units = g.Sum(x => x.Units) })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        int expired = 0, within7 = 0, within30 = 0, within60 = 0, within90 = 0, beyond90 = 0, noExpiry = 0;
        foreach (var row in expRaw)
        {
            if (row.ExpirationDate is not { } exp)
            {
                noExpiry += row.Units;
                continue;
            }

            var daysToExpiry = exp.DayNumber - today.DayNumber;
            if (daysToExpiry < 0) expired += row.Units;
            else if (daysToExpiry <= 7) within7 += row.Units;
            else if (daysToExpiry <= 30) within30 += row.Units;
            else if (daysToExpiry <= 60) within60 += row.Units;
            else if (daysToExpiry <= 90) within90 += row.Units;
            else beyond90 += row.Units;
        }

        var dto = new InventoryOverviewDto(
            new InventoryStockSummaryDto(
                onHand,
                reserved,
                onHand - reserved,
                distinctSkus,
                loadAgg?.Weight ?? 0m,
                loadAgg?.Volume ?? 0m),
            byZone.Select(x => new CompositionSliceDto(x.Key.ToString(), x.Units)).ToList(),
            byLocationType.Select(x => new CompositionSliceDto(x.Key.ToString(), x.Units)).ToList(),
            topProducts,
            new ExpiryBucketsDto(expired, within7, within30, within60, within90, beyond90, noExpiry));

        return dto;
    }
}
