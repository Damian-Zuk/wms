using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Dashboard.Queries;

public sealed record LocationsSummaryDto(
    int Total,
    int Active,
    int Inactive,
    int Blocked,
    int Occupied,
    int Empty);

/// <summary>Warehouse-wide utilization per dimension (only locations that cap that dimension count).</summary>
public sealed record CapacityUtilizationDto(
    int UnitsPercent,
    int WeightPercent,
    int VolumePercent,
    int UsedUnits,
    int UnitsCapacity,
    decimal UsedWeight,
    decimal WeightCapacity,
    decimal UsedVolume,
    decimal VolumeCapacity);

public sealed record LocationFillDto(
    Guid LocationId,
    string Code,
    string Address,
    int FillPercent,
    int OnHandUnits,
    bool IsBlocked);

public sealed record ZoneFillDto(string Zone, int FillPercent, int LocationCount);

public sealed record BlockedLocationDto(Guid LocationId, string Code, string Address, string? Reason);

public sealed record CapacityOverviewDto(
    LocationsSummaryDto Summary,
    CapacityUtilizationDto Utilization,
    IReadOnlyList<LocationFillDto> FullestLocations,
    IReadOnlyList<ZoneFillDto> ByZone,
    IReadOnlyList<BlockedLocationDto> BlockedLocations);

/// <summary>Location capacity/utilization metrics for the dashboard Capacity tab.</summary>
public sealed record GetCapacityOverviewQuery : IQuery<CapacityOverviewDto>;

public sealed class GetCapacityOverviewQueryHandler(IAppDbContext context)
    : IQueryHandler<GetCapacityOverviewQuery, CapacityOverviewDto>
{
    public async Task<Result<CapacityOverviewDto>> Handle(
        GetCapacityOverviewQuery query,
        CancellationToken cancellationToken)
    {
        // Per-location used load (units / weight / volume), only where stock is present.
        var loads = await context.Inventories
            .AsNoTracking()
            .Where(i => i.OnHand.Value > 0)
            .Join(context.Products, i => i.ProductId, p => p.Id,
                (i, p) => new
                {
                    i.LocationId,
                    Units = i.OnHand.Value,
                    Weight = i.OnHand.Value * p.Weight,
                    Volume = i.OnHand.Value * p.Volume
                })
            .GroupBy(x => x.LocationId)
            .Select(g => new
            {
                LocationId = g.Key,
                Units = g.Sum(x => x.Units),
                Weight = g.Sum(x => x.Weight),
                Volume = g.Sum(x => x.Volume)
            })
            .ToListAsync(cancellationToken);

        var loadByLocation = loads.ToDictionary(x => x.LocationId);

        // All (non-deleted) locations with capacities and address segments.
        var locations = await context.Locations
            .AsNoTracking()
            .Select(l => new
            {
                l.Id,
                Code = l.Code.Value,
                l.Address.Zone,
                l.Address.Aisle,
                l.Address.Rack,
                l.Address.Shelf,
                l.Address.Bin,
                l.IsActive,
                l.IsBlocked,
                l.BlockedReason,
                MaxUnits = l.Capacity.MaxUnits,
                MaxWeight = l.Capacity.MaxWeight,
                MaxVolume = l.Capacity.MaxVolume
            })
            .ToListAsync(cancellationToken);

        static int Pct(decimal used, decimal cap) => cap > 0 ? (int)Math.Round(used / cap * 100m) : 0;

        int totalUnitsUsed = 0, totalUnitsCap = 0;
        decimal totalWeightUsed = 0m, totalWeightCap = 0m;
        decimal totalVolumeUsed = 0m, totalVolumeCap = 0m;
        int active = 0, blocked = 0, occupied = 0;

        var fills = new List<(LocationFillDto Fill, string Zone, bool HasCap)>(locations.Count);

        foreach (var l in locations)
        {
            var load = loadByLocation.GetValueOrDefault(l.Id);
            var units = load?.Units ?? 0;
            var weight = load?.Weight ?? 0m;
            var volume = load?.Volume ?? 0m;

            if (l.IsActive) active++;
            if (l.IsBlocked) blocked++;
            if (units > 0) occupied++;

            // Most-restrictive configured dimension drives the location's fill %.
            decimal ratio = 0m;
            var hasCap = false;
            if (l.MaxUnits is int mu && mu > 0)
            {
                ratio = Math.Max(ratio, (decimal)units / mu);
                hasCap = true;
                totalUnitsUsed += units;
                totalUnitsCap += mu;
            }
            if (l.MaxWeight is decimal mw && mw > 0)
            {
                ratio = Math.Max(ratio, weight / mw);
                hasCap = true;
                totalWeightUsed += weight;
                totalWeightCap += mw;
            }
            if (l.MaxVolume is decimal mv && mv > 0)
            {
                ratio = Math.Max(ratio, volume / mv);
                hasCap = true;
                totalVolumeUsed += volume;
                totalVolumeCap += mv;
            }

            var address = $"{l.Zone}-{l.Aisle}-{l.Rack}-{l.Shelf}-{l.Bin}";
            fills.Add((
                new LocationFillDto(l.Id, l.Code, address, (int)Math.Round(ratio * 100m), units, l.IsBlocked),
                l.Zone,
                hasCap));
        }

        var summary = new LocationsSummaryDto(
            locations.Count,
            active,
            locations.Count - active,
            blocked,
            occupied,
            locations.Count - occupied);

        var utilization = new CapacityUtilizationDto(
            Pct(totalUnitsUsed, totalUnitsCap),
            Pct(totalWeightUsed, totalWeightCap),
            Pct(totalVolumeUsed, totalVolumeCap),
            totalUnitsUsed,
            totalUnitsCap,
            totalWeightUsed,
            totalWeightCap,
            totalVolumeUsed,
            totalVolumeCap);

        var fullest = fills
            .Where(f => f.HasCap)
            .OrderByDescending(f => f.Fill.FillPercent)
            .ThenByDescending(f => f.Fill.OnHandUnits)
            .Take(8)
            .Select(f => f.Fill)
            .ToList();

        var byZone = fills
            .GroupBy(f => f.Zone)
            .Select(g => new ZoneFillDto(
                g.Key,
                (int)Math.Round(g.Average(f => f.Fill.FillPercent)),
                g.Count()))
            .OrderBy(z => z.Zone)
            .ToList();

        var blockedLocations = fills
            .Where(f => f.Fill.IsBlocked)
            .Select(f => new BlockedLocationDto(
                f.Fill.LocationId, f.Fill.Code, f.Fill.Address, null))
            .ToList();

        // Attach blocked reasons (kept out of the fill projection to stay light).
        if (blockedLocations.Count > 0)
        {
            var reasons = locations
                .Where(l => l.IsBlocked)
                .ToDictionary(l => l.Id, l => l.BlockedReason);
            blockedLocations = blockedLocations
                .Select(b => b with { Reason = reasons.GetValueOrDefault(b.LocationId) })
                .ToList();
        }

        var dto = new CapacityOverviewDto(summary, utilization, fullest, byZone, blockedLocations);
        return dto;
    }
}
