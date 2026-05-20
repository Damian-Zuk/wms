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

        var locationIds = stockIn.Items.Select(i => i.LocationId).Distinct().ToList();
        var productIds = stockIn.Items.Select(i => i.ProductId).Distinct().ToList();
        var lotIds = stockIn.Items.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var locations = await context.Locations
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var products = await context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var lots = lotIds.Count > 0
            ? await context.Lots
                .Where(l => lotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken)
            : new Dictionary<Guid, Lot>();

        var inventoriesAtLocations = await context.Inventories
            .Where(i => locationIds.Contains(i.LocationId))
            .ToListAsync(cancellationToken);

        var result = stockIn.Receive();
        if (result.IsFailure)
            return result;

        foreach (var item in stockIn.Items)
        {
            if (!locations.TryGetValue(item.LocationId, out var location))
                return StockInErrors.LocationNotFound(item.LocationId);

            if (!products.TryGetValue(item.ProductId, out var product))
                return StockInErrors.ProductNotFound(item.ProductId);

            Lot? lot = null;
            if (item.LotId.HasValue && !lots.TryGetValue(item.LotId.Value, out lot))
                return StockInErrors.LotNotFound(item.LotId.Value);

            var contentsAtLocation = inventoriesAtLocations
                .Where(i => i.LocationId == item.LocationId)
                .ToList();

            var canAccept = location.CanAccept(product, lot, item.Quantity, contentsAtLocation);
            if (canAccept.IsFailure)
                return canAccept;

            var inventory = inventoriesAtLocations.FirstOrDefault(i =>
                i.LocationId == item.LocationId
                && i.ProductId == item.ProductId
                && i.LotId == item.LotId);

            if (inventory is null)
            {
                inventory = new Inventory(item.ProductId, item.LocationId, item.LotId);
                await context.Inventories.AddAsync(inventory, cancellationToken);
                inventoriesAtLocations.Add(inventory);
            }

            inventory.Increase(item.Quantity);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
