using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Commands;

public sealed record StartPickingStockOutCommand(Guid Id) : ICommand;

public sealed class StartPickingStockOutCommandHandler(IAppDbContext context)
    : ICommandHandler<StartPickingStockOutCommand>
{
    public async Task<Result> Handle(StartPickingStockOutCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.Id);

        var transition = stockOut.StartPicking();
        if (transition.IsFailure)
            return transition;

        foreach (var item in stockOut.Items)
        {
            var inventory = await context.Inventories
                .FirstOrDefaultAsync(
                    inv => inv.ProductId == item.ProductId
                        && inv.LocationId == item.LocationId
                        && inv.LotId == item.LotId,
                    cancellationToken);

            if (inventory is null || inventory.Quantity.Value < item.Quantity.Value)
                return StockOutErrors.InsufficientInventory(item.ProductId, item.LocationId);

            inventory.Decrease(item.Quantity);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
