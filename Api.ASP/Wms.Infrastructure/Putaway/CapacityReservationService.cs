using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wms.Application.Putaway;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Services;
using Wms.Infrastructure.Data;
using Wms.Shared.Common;

namespace Wms.Infrastructure.Putaway;

/// <summary>
/// Verifies and reserves capacity for a draft stock-in inside a single transaction.
/// The involved location rows are locked with <c>SELECT … FOR UPDATE</c> so two
/// stock-ins racing to put away into the same location are serialised: the first
/// wins, the second sees the reserved space and fails (the stock-in stays Draft).
/// </summary>
internal sealed class CapacityReservationService(AppDbContext db) : ICapacityReservationService
{
    public async Task<Result> ReserveForStartPutawayAsync(Guid stockInId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var stockIn = await db.StockIns
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == stockInId, ct);

        if (stockIn is null)
            return await RollbackAsync(transaction, StockInErrors.NotFound(stockInId), ct);

        if (stockIn.Status != StockInStatus.Draft)
            return await RollbackAsync(
                transaction,
                StockInErrors.InvalidStatusTransition(stockIn.Status, StockInStatus.Putaway),
                ct);

        var placements = stockIn.Lines.SelectMany(l => l.Items).ToList();
        var locationIds = placements.Select(i => i.LocationId).Distinct().ToList();

        // Lock the involved location rows for the duration of the transaction.
        // ORDER BY "Id" keeps lock acquisition deterministic to avoid deadlocks
        // between two stock-ins that share more than one location. The raw query is
        // run without composition (IgnoreQueryFilters + no extra LINQ) so the
        // FOR UPDATE clause reaches Postgres intact; the soft-delete predicate is
        // therefore spelled out explicitly.
        var lockedLocations = await db.Locations
            .FromSqlRaw(
                """SELECT * FROM "Locations" WHERE "Id" = ANY({0}) AND "IsDeleted" = false ORDER BY "Id" FOR UPDATE""",
                locationIds.ToArray())
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        var locations = lockedLocations.ToDictionary(l => l.Id);

        var lotIds = stockIn.Lines.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();

        var lots = lotIds.Count > 0
            ? await db.Lots.Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct)
            : [];

        var contentsByLocation = (await db.Inventories
                .Where(i => locationIds.Contains(i.LocationId))
                .ToListAsync(ct))
            .GroupBy(i => i.LocationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var otherActiveReservations = await db.CapacityReservations
            .Where(r =>
                locationIds.Contains(r.LocationId) &&
                r.StockInId != stockInId)
            .ToListAsync(ct);

        // Every product occupying these locations (this stock-in's lines + existing
        // contents + other stock-ins' reservations), needed to weigh load on the
        // Weight/Volume dimensions.
        var productIds = stockIn.Lines.Select(l => l.ProductId)
            .Concat(contentsByLocation.Values.SelectMany(c => c).Select(i => i.ProductId))
            .Concat(otherActiveReservations.Select(r => r.ProductId))
            .Distinct()
            .ToList();

        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // Per-location occupancy, seeded with other stock-ins' active reservations,
        // then grown as we fold in this stock-in's own placements.
        var occupancyByLocation = new Dictionary<Guid, CapacityOccupancy>();
        foreach (var reservation in otherActiveReservations)
            if (products.TryGetValue(reservation.ProductId, out var reservationProduct))
                OccupancyFor(occupancyByLocation, reservation.LocationId)
                    .Add(CapacityLoadCalculator.Load(reservationProduct, reservation.Quantity));

        var reservations = new List<CapacityReservation>();

        foreach (var line in stockIn.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                return await RollbackAsync(transaction, StockInErrors.ProductNotFound(line.ProductId), ct);

            Lot? lot = null;
            if (line.LotId.HasValue && !lots.TryGetValue(line.LotId.Value, out lot))
                return await RollbackAsync(transaction, StockInErrors.LotNotFound(line.LotId.Value), ct);

            foreach (var item in line.Items)
            {
                if (!locations.TryGetValue(item.LocationId, out var location))
                    return await RollbackAsync(transaction, StockInErrors.LocationNotFound(item.LocationId), ct);

                var contents = contentsByLocation.TryGetValue(item.LocationId, out var c) ? c : [];
                var occupancy = OccupancyFor(occupancyByLocation, item.LocationId);

                // Surface the failure as-is: a capacity clash returns the dimension-aware
                // Location.CapacityExceeded error so the user sees which limit was hit.
                var canAccept = location.CanAccept(product, lot, item.Quantity, contents, occupancy, products);
                if (canAccept.IsFailure)
                    return await RollbackAsync(transaction, canAccept.Error, ct);

                occupancy.Add(CapacityLoadCalculator.Load(product, item.Quantity));

                reservations.Add(new CapacityReservation(
                    stockIn.Id,
                    item.Id,
                    item.LocationId,
                    line.ProductId,
                    line.LotId,
                    item.Quantity));
            }
        }

        var startResult = stockIn.StartPutaway();
        if (startResult.IsFailure)
            return await RollbackAsync(transaction, startResult.Error, ct);

        await db.CapacityReservations.AddRangeAsync(reservations, ct);

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return InventoryErrors.ConcurrencyConflict();
        }
    }

    private static CapacityOccupancy OccupancyFor(Dictionary<Guid, CapacityOccupancy> map, Guid locationId)
    {
        if (!map.TryGetValue(locationId, out var occupancy))
        {
            occupancy = new CapacityOccupancy();
            map[locationId] = occupancy;
        }

        return occupancy;
    }

    private static async Task<Result> RollbackAsync(IDbContextTransaction transaction, Error error, CancellationToken ct)
    {
        await transaction.RollbackAsync(ct);
        return error;
    }
}
