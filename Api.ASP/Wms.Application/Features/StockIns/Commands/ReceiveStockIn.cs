using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Commands;

public sealed record ReceiveStockInCommand(Guid Id) : ICommand;

public sealed class ReceiveStockInCommandHandler(IAppDbContext context)
    : ICommandHandler<ReceiveStockInCommand>
{
    public async Task<Result> Handle(ReceiveStockInCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.Id);

        var result = stockIn.Receive();
        if (result.IsFailure)
            return result;

        foreach (var item in stockIn.Items)
        {
            var inventory = await context.Inventories
                .FirstOrDefaultAsync(
                    inv => inv.ProductId == item.ProductId
                        && inv.LocationId == item.LocationId
                        && inv.LotId == item.LotId,
                    cancellationToken);

            if (inventory is null)
            {
                inventory = new Inventory(item.ProductId, item.LocationId, item.LotId);
                await context.Inventories.AddAsync(inventory, cancellationToken);
            }

            inventory.Increase(item.Quantity);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
