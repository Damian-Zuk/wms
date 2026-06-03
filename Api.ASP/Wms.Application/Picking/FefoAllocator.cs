using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Picking;

internal sealed class FefoAllocator(IAppDbContext context) : IFefoAllocator
{
    public async Task<Result<IReadOnlyList<LotAllocation>>> AllocateAsync(
        Guid productId,
        Guid? locationId,
        Quantity required,
        CancellationToken ct)
    {
        // Inventory rows for the product that are lot-tracked
        // (LotId is not null) and have something to give (Available > 0).
        var inventoryQuery = context.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == productId && i.LotId != null);

        if (locationId.HasValue)
            inventoryQuery = inventoryQuery.Where(i => i.LocationId == locationId.Value);

        // Project to the fields FEFO needs and filter Available > 0 server-side.
        var rows = await inventoryQuery
            .Where(i => i.OnHand.Value - i.Reserved.Value > 0)
            .Select(i => new
            {
                i.LotId,
                Available = i.OnHand.Value - i.Reserved.Value
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return InventoryErrors.InsufficientAvailableStockForFefo(productId, 0, required.Value);

        // Pull the matching Lot rows so we can sort by expiration. Lots
        // without an ExpirationDate sort LAST. Tie-break by LotNumber
        // ascending for determinism so repeated allocations behave the same.
        var lotIds = rows.Select(r => r.LotId!.Value).Distinct().ToList();

        var lotsOrdered = await context.Lots
            .AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .OrderBy(l => l.ExpirationDate.HasValue ? 0 : 1)
                .ThenBy(l => l.ExpirationDate)
                .ThenBy(l => l.Number.Value)
            .Select(l => new { l.Id })
            .ToListAsync(ct);

        // Pair lots with their Available totals (summed across rows in
        // case the same product+lot exists in multiple locations and
        // locationId was null).
        var availableByLot = rows
            .GroupBy(r => r.LotId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Available));

        var totalAvailable = availableByLot.Values.Sum();
        if (totalAvailable < required.Value)
            return InventoryErrors.InsufficientAvailableStockForFefo(productId, totalAvailable, required.Value);

        // Greedy walk: take everything from each lot in expiry order
        // until the request is satisfied; the last lot may contribute a
        // partial amount.
        var remaining = required.Value;
        var allocations = new List<LotAllocation>();

        foreach (var lot in lotsOrdered)
        {
            if (remaining == 0)
                break;

            if (!availableByLot.TryGetValue(lot.Id, out var lotAvailable) || lotAvailable <= 0)
                continue;

            var take = Math.Min(remaining, lotAvailable);
            allocations.Add(new LotAllocation(lot.Id, new Quantity(take)));
            remaining -= take;
        }

        if (remaining > 0)
        {
            // Should be unreachable given the totalAvailable check above.
            return InventoryErrors.InsufficientAvailableStockForFefo(productId, totalAvailable, required.Value);
        }

        return Result.Success<IReadOnlyList<LotAllocation>>(allocations);
    }
}
