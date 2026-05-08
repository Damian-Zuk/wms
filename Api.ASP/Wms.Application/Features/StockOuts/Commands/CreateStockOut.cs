using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Commands;

public sealed record StockOutItemRequest(Guid ProductId, Guid LocationId, Guid? LotId, int Quantity);

public sealed record CreateStockOutCommand(List<StockOutItemRequest> Items) : ICommand<Guid>;

public sealed class CreateStockOutValidator : AbstractValidator<CreateStockOutCommand>
{
    public CreateStockOutValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
            item.RuleFor(x => x.LocationId).NotEmpty().WithMessage("Location ID is required");
            item.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
    }
}

public sealed class CreateStockOutCommandHandler(IAppDbContext context)
    : ICommandHandler<CreateStockOutCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateStockOutCommand request, CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var locationIds = request.Items.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = request.Items.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var existingProductIds = await context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var missingProduct = productIds.Except(existingProductIds).FirstOrDefault();
        if (missingProduct != default)
            return Error.Problem("StockOut.ProductNotFound", $"Product {missingProduct} not found.");

        var existingLocationIds = await context.Locations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var missingLocation = locationIds.Except(existingLocationIds).FirstOrDefault();
        if (missingLocation != default)
            return Error.Problem("StockOut.LocationNotFound", $"Location {missingLocation} not found.");

        if (lotIds.Count > 0)
        {
            var existingLotIds = await context.Lots
                .AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            var missingLot = lotIds.Except(existingLotIds).FirstOrDefault();
            if (missingLot != default)
                return Error.Problem("StockOut.LotNotFound", $"Lot {missingLot} not found.");
        }

        var stockOut = new StockOut(Guid.NewGuid());

        foreach (var item in request.Items)
        {
            var qty = new Quantity(item.Quantity);

            var inventory = await context.Inventories
                .FirstOrDefaultAsync(
                    inv => inv.ProductId == item.ProductId
                        && inv.LocationId == item.LocationId
                        && inv.LotId == item.LotId,
                    cancellationToken);

            if (inventory is null || inventory.Quantity.Value < item.Quantity)
                return Error.Problem(
                    "StockOut.InsufficientInventory",
                    $"Insufficient inventory for product {item.ProductId} at location {item.LocationId}.");

            stockOut.AddItem(item.ProductId, item.LocationId, item.LotId, qty);
            inventory.Decrease(qty);

            var movement = new StockMovement(
                item.ProductId,
                item.LocationId,
                item.LotId,
                item.Quantity,
                StockMovementType.Out,
                StockMovementSource.StockOut,
                stockOut.Id);

            await context.StockMovements.AddAsync(movement, cancellationToken);
        }

        await context.StockOuts.AddAsync(stockOut, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return stockOut.Id;
    }
}
