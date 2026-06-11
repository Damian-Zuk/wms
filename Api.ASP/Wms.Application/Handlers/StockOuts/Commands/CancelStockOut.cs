using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

public sealed record CancelStockOutCommand(Guid Id) : ICommand;

/// <summary>
/// Cancels a stock-out (Draft or Picking only). For every item it releases the
/// still-reserved remainder, and — when cancelling from Picking — returns the
/// already-picked units to stock (the domain raises the return-to-stock event that
/// writes the StockMovement(In) audit row).
/// </summary>
public sealed class CancelStockOutCommandHandler(IAppDbContext context)
    : ICommandHandler<CancelStockOutCommand>
{
    public async Task<Result> Handle(CancelStockOutCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.Id);

        var transition = stockOut.Cancel();
        if (transition.IsFailure)
            return transition;

        var productIds = stockOut.Lines.Select(l => l.ProductId).Distinct().ToList();
        var locationIds = stockOut.Lines.SelectMany(l => l.Items).Select(i => i.LocationId).Distinct().ToList();

        var inventories = await context.Inventories
            .Where(i => productIds.Contains(i.ProductId) && locationIds.Contains(i.LocationId))
            .ToListAsync(cancellationToken);

        foreach (var line in stockOut.Lines)
        {
            foreach (var item in line.Items)
            {
                var inventory = inventories.FirstOrDefault(i =>
                    i.ProductId == line.ProductId
                    && i.LocationId == item.LocationId
                    && i.LotId == item.LotId);

                // Release the reservation on units that were never picked.
                if (item.Remaining > 0 && inventory is not null)
                {
                    var release = inventory.ReleaseReservation(new Quantity(item.Remaining));
                    if (release.IsFailure)
                        return release;
                }

                // Return the already-picked units to stock (recreate the row if it vanished).
                if (item.PickedQuantity.Value > 0)
                {
                    if (inventory is null)
                    {
                        inventory = new Inventory(line.ProductId, item.LocationId, item.LotId);
                        await context.Inventories.AddAsync(inventory, cancellationToken);
                        inventories.Add(inventory);
                    }

                    inventory.Increase(new Quantity(item.PickedQuantity.Value));
                }
            }
        }

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
