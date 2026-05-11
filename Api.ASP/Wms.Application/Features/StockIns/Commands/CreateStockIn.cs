using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Commands;

public sealed record StockInItemRequest(Guid ProductId, Guid LocationId, Guid? LotId, int Quantity);

public sealed record CreateStockInCommand(List<StockInItemRequest> Items) : ICommand<Guid>;

public sealed class CreateStockInValidator : AbstractValidator<CreateStockInCommand>
{
    public CreateStockInValidator()
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

public sealed class CreateStockInCommandHandler(IAppDbContext context)
    : ICommandHandler<CreateStockInCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateStockInCommand request, CancellationToken cancellationToken)
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
            return Error.Problem("StockIn.ProductNotFound", $"Product {missingProduct} not found.");

        var existingLocationIds = await context.Locations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var missingLocation = locationIds.Except(existingLocationIds).FirstOrDefault();
        if (missingLocation != default)
            return Error.Problem("StockIn.LocationNotFound", $"Location {missingLocation} not found.");

        if (lotIds.Count > 0)
        {
            var existingLotIds = await context.Lots
                .AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            var missingLot = lotIds.Except(existingLotIds).FirstOrDefault();
            if (missingLot != default)
                return Error.Problem("StockIn.LotNotFound", $"Lot {missingLot} not found.");
        }

        var stockIn = new StockIn(Guid.NewGuid());

        foreach (var item in request.Items)
        {
            var qty = new Quantity(item.Quantity);
            stockIn.AddItem(item.ProductId, item.LocationId, item.LotId, qty);

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

            inventory.Increase(qty);
        }

        await context.StockIns.AddAsync(stockIn, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return stockIn.Id;
    }
}
