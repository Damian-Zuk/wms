using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

/// <summary>Request body for putting away a placement (route supplies the ids).</summary>
public sealed record PutawayStockInItemRequest(int Quantity);

public sealed record PutawayStockInItemCommand(Guid StockInId, Guid ItemId, int Quantity) : ICommand;

public sealed class PutawayStockInItemValidator : AbstractValidator<PutawayStockInItemCommand>
{
    public PutawayStockInItemValidator()
    {
        RuleFor(x => x.StockInId).NotEmpty().WithMessage("StockIn ID is required");
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Item ID is required");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

/// <summary>
/// Books the manual putaway of one placement (whole or partial). The placed units
/// become on-hand inventory and the placement's capacity hold shrinks by the same
/// amount, both in a single save so the space is never double-counted.
/// </summary>
public sealed class PutawayStockInItemCommandHandler(IAppDbContext context)
    : ICommandHandler<PutawayStockInItemCommand>
{
    public async Task<Result> Handle(PutawayStockInItemCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == command.StockInId, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.StockInId);

        var line = stockIn.Lines.FirstOrDefault(l => l.Items.Any(i => i.Id == command.ItemId));
        if (line is null)
            return StockInErrors.ItemNotFound(command.ItemId);

        var item = line.Items.First(i => i.Id == command.ItemId);
        var quantity = new Quantity(command.Quantity);

        // Validate the destination can still take these units (mirrors the old receive).
        var location = await context.Locations
            .FirstOrDefaultAsync(l => l.Id == item.LocationId, cancellationToken);
        if (location is null)
            return StockInErrors.LocationNotFound(item.LocationId);

        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == line.ProductId, cancellationToken);
        if (product is null)
            return StockInErrors.ProductNotFound(line.ProductId);

        Lot? lot = null;
        if (line.LotId.HasValue)
        {
            lot = await context.Lots.FirstOrDefaultAsync(l => l.Id == line.LotId.Value, cancellationToken);
            if (lot is null)
                return StockInErrors.LotNotFound(line.LotId.Value);
        }

        var contentsAtLocation = await context.Inventories
            .Where(i => i.LocationId == item.LocationId)
            .ToListAsync(cancellationToken);

        var canAccept = location.CanAccept(product, lot, quantity, contentsAtLocation);
        if (canAccept.IsFailure)
            return canAccept;

        // Record the putaway on the placement (raises a stock-movement event).
        var putaway = stockIn.PutawayItem(command.ItemId, quantity);
        if (putaway.IsFailure)
            return putaway;

        // Book the units on hand.
        var inventory = contentsAtLocation.FirstOrDefault(i =>
            i.LocationId == item.LocationId
            && i.ProductId == line.ProductId
            && i.LotId == line.LotId);

        if (inventory is null)
        {
            inventory = new Inventory(line.ProductId, item.LocationId, line.LotId);
            await context.Inventories.AddAsync(inventory, cancellationToken);
        }

        inventory.Receive(quantity, DateTime.UtcNow);

        // Release the matching capacity hold by the same amount; drop it once empty.
        var reservation = await context.CapacityReservations
            .FirstOrDefaultAsync(
                r => r.StockInId == stockIn.Id && r.StockInItemId == item.Id,
                cancellationToken);

        if (reservation is not null)
        {
            var reduce = reservation.Reduce(quantity);
            if (reduce.IsFailure)
                return reduce;

            if (reservation.Quantity.Value == 0)
                context.CapacityReservations.Remove(reservation);
        }

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
