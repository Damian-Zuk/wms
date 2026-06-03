using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Extensions;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

public sealed record PackStockOutCommand(Guid Id) : ICommand;

/// <summary>
/// Transitions a Picking stock-out into Packed. This is where the physical
/// stock is actually removed (OnHand and Reserved both drop). The status
/// transition raises a picked event per item, which the domain-event
/// handler turns into a StockMovement(Out) audit row.
/// </summary>
public sealed class PackStockOutCommandHandler(IAppDbContext context)
    : ICommandHandler<PackStockOutCommand>
{
    public async Task<Result> Handle(PackStockOutCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.Id);

        var locationIds = stockOut.Items.Select(i => i.LocationId).Distinct().ToList();
        var productIds = stockOut.Items.Select(i => i.ProductId).Distinct().ToList();

        var inventories = await context.Inventories
            .Where(i => locationIds.Contains(i.LocationId) && productIds.Contains(i.ProductId))
            .ToListAsync(cancellationToken);

        // Pass 1 — validation. Every line must map to an existing
        // Inventory row, and that row must hold enough Reserved/OnHand for
        // its quantity. We do not mutate inventory yet.
        foreach (var item in stockOut.Items)
        {
            var inventory = inventories.FirstOrDefault(i =>
                i.ProductId == item.ProductId
                && i.LocationId == item.LocationId
                && i.LotId == item.LotId);

            if (inventory is null)
                return StockOutErrors.InsufficientInventory(item.ProductId, item.LocationId);

            if (item.Quantity.Value > inventory.Reserved.Value
                || item.Quantity.Value > inventory.OnHand.Value)
                return StockOutErrors.InsufficientInventory(item.ProductId, item.LocationId);
        }

        // Pass 2 — status transition. Raises picked events that the
        // dispatcher will turn into StockMovement(Out) rows.
        var transition = stockOut.Pack();
        if (transition.IsFailure)
            return transition;

        // Pass 3 — apply the picks.
        foreach (var item in stockOut.Items)
        {
            var inventory = inventories.First(i =>
                i.ProductId == item.ProductId
                && i.LocationId == item.LocationId
                && i.LotId == item.LotId);

            var pickResult = inventory.Pick(item.Quantity);
            if (pickResult.IsFailure)
                return pickResult;
        }

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
