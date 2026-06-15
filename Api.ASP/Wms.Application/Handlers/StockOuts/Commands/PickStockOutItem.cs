using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

/// <summary>Request body for picking an item (route supplies the ids).</summary>
public sealed record PickStockOutItemRequest(int Quantity);

public sealed record PickStockOutItemCommand(Guid StockOutId, Guid ItemId, int Quantity) : ICommand;

public sealed class PickStockOutItemValidator : AbstractValidator<PickStockOutItemCommand>
{
    public PickStockOutItemValidator()
    {
        RuleFor(x => x.StockOutId).NotEmpty().WithMessage("StockOut ID is required");
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Item ID is required");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

/// <summary>
/// Books the manual pick of one item (whole or partial). The picked units are removed
/// from inventory immediately — both OnHand and Reserved drop — and a picked event
/// drives the StockMovement(Out) audit row, all in a single save.
/// </summary>
public sealed class PickStockOutItemCommandHandler(IAppDbContext context)
    : ICommandHandler<PickStockOutItemCommand>
{
    public async Task<Result> Handle(PickStockOutItemCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == command.StockOutId, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.StockOutId);

        var line = stockOut.Lines.FirstOrDefault(l => l.Items.Any(i => i.Id == command.ItemId));
        if (line is null)
            return StockOutErrors.ItemNotFound(command.ItemId);

        var item = line.Items.First(i => i.Id == command.ItemId);
        var quantity = new Quantity(command.Quantity);

        // The inventory row the pick draws from (the planner pinned location + lot +
        // handling unit).
        var inventory = await context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == line.ProductId
                    && i.LocationId == item.LocationId
                    && i.LotId == item.LotId
                    && i.HandlingUnitId == item.HandlingUnitId,
                cancellationToken);

        if (inventory is null)
            return StockOutErrors.InsufficientInventory(line.ProductId, item.LocationId);

        // Record the pick on the item (raises a stock-movement event for the qty).
        var pick = stockOut.PickItem(command.ItemId, quantity);
        if (pick.IsFailure)
            return pick;

        // Remove the units now: OnHand and Reserved both drop.
        var inventoryPick = inventory.Pick(quantity);
        if (inventoryPick.IsFailure)
            return inventoryPick;

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
