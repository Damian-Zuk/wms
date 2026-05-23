using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Enums;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Commands;

public sealed record CancelStockOutCommand(Guid Id) : ICommand;

public sealed class CancelStockOutCommandHandler(IAppDbContext context)
    : ICommandHandler<CancelStockOutCommand>
{
    public async Task<Result> Handle(CancelStockOutCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.Id);

        var statusBeforeCancel = stockOut.Status;

        var transition = stockOut.Cancel();
        if (transition.IsFailure)
            return transition;

        var locationIds = stockOut.Items.Select(i => i.LocationId).Distinct().ToList();
        var productIds = stockOut.Items.Select(i => i.ProductId).Distinct().ToList();

        var inventories = await context.Inventories
            .Where(i => locationIds.Contains(i.LocationId) && productIds.Contains(i.ProductId))
            .ToListAsync(cancellationToken);

        foreach (var item in stockOut.Items)
        {
            var inventory = inventories.FirstOrDefault(i =>
                i.ProductId == item.ProductId
                && i.LocationId == item.LocationId
                && i.LotId == item.LotId);

            if (statusBeforeCancel == StockOutStatus.Draft)
            {
                if (inventory is null)
                    continue;

                var release = inventory.ReleaseReservation(item.Quantity);
                if (release.IsFailure)
                    return release;
            }
            else
            {
                if (inventory is null)
                {
                    inventory = new Inventory(item.ProductId, item.LocationId, item.LotId);
                    await context.Inventories.AddAsync(inventory, cancellationToken);
                    inventories.Add(inventory);
                }

                inventory.Increase(item.Quantity);
            }
        }

        var saveResult = await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
        return saveResult;
    }
}
