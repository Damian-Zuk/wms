using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record ReceiveStockInCommand(Guid Id) : ICommand;

public sealed class ReceiveStockInCommandHandler(IAppDbContext context)
    : ICommandHandler<ReceiveStockInCommand>
{
    public async Task<Result> Handle(ReceiveStockInCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.Id);

        var placements = stockIn.Lines
            .SelectMany(l => l.Items.Select(i => (Line: l, Item: i)))
            .ToList();

        var locationIds = placements.Select(p => p.Item.LocationId).Distinct().ToList();
        var productIds = stockIn.Lines.Select(l => l.ProductId).Distinct().ToList();
        var lotIds = stockIn.Lines.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();

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
            : [];

        var inventoriesAtLocations = await context.Inventories
            .Where(i => locationIds.Contains(i.LocationId))
            .ToListAsync(cancellationToken);

        // Pass 1 — validation only.
        foreach (var (line, item) in placements)
        {
            if (!locations.TryGetValue(item.LocationId, out var location))
                return StockInErrors.LocationNotFound(item.LocationId);

            if (!products.TryGetValue(line.ProductId, out var product))
                return StockInErrors.ProductNotFound(line.ProductId);

            Lot? lot = null;
            if (line.LotId.HasValue && !lots.TryGetValue(line.LotId.Value, out lot))
                return StockInErrors.LotNotFound(line.LotId.Value);

            var contentsAtLocation = inventoriesAtLocations
                .Where(i => i.LocationId == item.LocationId)
                .ToList();

            var canAccept = location.CanAccept(product, lot, item.Quantity, contentsAtLocation);
            if (canAccept.IsFailure)
                return canAccept;
        }

        // Pass 2 — status transition (raises a received event per placement).
        var result = stockIn.Receive();
        if (result.IsFailure)
            return result;

        // Pass 3 — mutate inventory.
        foreach (var (line, item) in placements)
        {
            var inventory = inventoriesAtLocations.FirstOrDefault(i =>
                i.LocationId == item.LocationId
                && i.ProductId == line.ProductId
                && i.LotId == line.LotId);

            if (inventory is null)
            {
                inventory = new Inventory(line.ProductId, item.LocationId, line.LotId);
                await context.Inventories.AddAsync(inventory, cancellationToken);
                inventoriesAtLocations.Add(inventory);
            }

            inventory.Increase(item.Quantity);
        }

        // Pass 4 — delete the capacity reservations now that the units are on hand.
        // OnHand goes up and the holds disappear in the same save, so other
        // stock-ins never double-count the space.
        var reservations = await context.CapacityReservations
            .Where(r => r.StockInId == stockIn.Id)
            .ToListAsync(cancellationToken);

        context.CapacityReservations.RemoveRange(reservations);

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
