using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record CancelStockInCommand(Guid Id) : ICommand;

/// <summary>
/// Cancels a stock-in (Draft or Putaway only). It releases the still-held capacity
/// reservations, and — when cancelling from Putaway — removes the already-placed
/// units from inventory (the domain raises the removed-from-stock event that writes
/// the StockMovement(Out) audit row).
/// </summary>
public sealed class CancelStockInCommandHandler(IAppDbContext context)
    : ICommandHandler<CancelStockInCommand>
{
    public async Task<Result> Handle(CancelStockInCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.Id);

        var transition = stockIn.Cancel();
        if (transition.IsFailure)
            return transition;

        // Release the remaining capacity holds. Only the not-yet-placed holds survive
        // to cancel time (putaway drops a hold as it places); a Draft cancel has none.
        var reservations = await context.CapacityReservations
            .Where(r => r.StockInId == stockIn.Id)
            .ToListAsync(cancellationToken);

        context.CapacityReservations.RemoveRange(reservations);

        // Pull the already-placed units back out of inventory.
        var productIds = stockIn.Lines.Select(l => l.ProductId).Distinct().ToList();
        var locationIds = stockIn.Lines.SelectMany(l => l.Items).Select(i => i.LocationId).Distinct().ToList();

        var inventories = await context.Inventories
            .Where(i => productIds.Contains(i.ProductId) && locationIds.Contains(i.LocationId))
            .ToListAsync(cancellationToken);

        foreach (var line in stockIn.Lines)
        {
            foreach (var item in line.Items.Where(i => i.PlacedQuantity.Value > 0))
            {
                var inventory = inventories.FirstOrDefault(i =>
                    i.ProductId == line.ProductId
                    && i.LocationId == item.LocationId
                    && i.LotId == line.LotId
                    && i.HandlingUnitId == item.HandlingUnitId);

                // The putaway created this row, so it should exist; treat a missing or
                // short row as the units no longer being on hand to remove.
                if (inventory is null)
                    return InventoryErrors.InsufficientQuantity(0, item.PlacedQuantity.Value);

                var decrease = inventory.Decrease(new Quantity(item.PlacedQuantity.Value));
                if (decrease.IsFailure)
                    return decrease;
            }
        }

        await CleanUpDeclaredHandlingUnitsAsync(stockIn, cancellationToken);

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }

    /// <summary>
    /// Soft-deletes the stock-in's declared handling units that end up holding nothing —
    /// never-placed ones and ones whose putaway was just reversed. A unit that gained
    /// other stock in the meantime (someone packed onto it) stays.
    /// </summary>
    private async Task CleanUpDeclaredHandlingUnitsAsync(StockIn stockIn, CancellationToken cancellationToken)
    {
        var huIds = stockIn.Lines
            .SelectMany(l => l.Items)
            .Where(i => i.HandlingUnitId.HasValue)
            .Select(i => i.HandlingUnitId!.Value)
            .Distinct()
            .ToList();

        if (huIds.Count == 0)
            return;

        var handlingUnits = await context.HandlingUnits
            .Where(h => huIds.Contains(h.Id))
            .ToListAsync(cancellationToken);

        // The identity map returns the rows already decreased above, so these sums
        // reflect the post-reversal state.
        var stockByHu = (await context.Inventories
                .Where(i => i.HandlingUnitId.HasValue && huIds.Contains(i.HandlingUnitId.Value))
                .ToListAsync(cancellationToken))
            .GroupBy(i => i.HandlingUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.OnHand.Value + i.Reserved.Value));

        foreach (var handlingUnit in handlingUnits)
        {
            var remaining = stockByHu.TryGetValue(handlingUnit.Id, out var total) ? total : 0;
            if (remaining == 0)
                handlingUnit.MarkAsDeleted();
        }
    }
}
