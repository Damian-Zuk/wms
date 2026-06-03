using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Picking;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;
using Wms.Application.Extensions;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Data;

namespace Wms.Application.Handlers.StockOuts.Commands;

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

public sealed class CreateStockOutCommandHandler(
    IAppDbContext context,
    IFefoAllocator fefoAllocator)
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
            return StockOutErrors.ProductNotFound(missingProduct);

        var existingLocationIds = await context.Locations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var missingLocation = locationIds.Except(existingLocationIds).FirstOrDefault();
        if (missingLocation != default)
            return StockOutErrors.LocationNotFound(missingLocation);

        if (lotIds.Count > 0)
        {
            var existingLotIds = await context.Lots
                .AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            var missingLot = lotIds.Except(existingLotIds).FirstOrDefault();
            if (missingLot != default)
                return StockOutErrors.LotNotFound(missingLot);
        }

        var lotTrackedProductIds = (await context.Lots
                .AsNoTracking()
                .Where(l => productIds.Contains(l.ProductId))
                .Select(l => l.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var inventories = await context.Inventories
            .Where(i => locationIds.Contains(i.LocationId) && productIds.Contains(i.ProductId))
            .ToListAsync(cancellationToken);

        // Pass 1 — build the reservation plan without mutating any
        // inventory rows. Each planned reservation pins the exact Inventory
        // entity that will be reserved; the second pass simply applies them.
        // This means a mid-list failure leaves all earlier inventory rows
        // exactly as we read them, instead of relying on EF discarding
        // tracked mutations on the failure return.
        var plan = new List<(Inventory Inventory, Quantity Quantity, Guid? LotId, Guid ProductId, Guid LocationId)>();

        foreach (var item in request.Items)
        {
            // FEFO branch: lot-tracked product, caller didn't pin a lot.
            if (!item.LotId.HasValue && lotTrackedProductIds.Contains(item.ProductId))
            {
                var fefo = await fefoAllocator.AllocateAsync(
                    item.ProductId,
                    item.LocationId,
                    new Quantity(item.Quantity),
                    cancellationToken);

                if (fefo.IsFailure)
                    return Result.Failure<Guid>(fefo.Error);

                foreach (var allocation in fefo.Value)
                {
                    var allocInventory = inventories.FirstOrDefault(i =>
                        i.ProductId == item.ProductId
                        && i.LocationId == item.LocationId
                        && i.LotId == allocation.LotId);

                    if (allocInventory is null)
                    {
                        // FEFO promised this lot has Available; if the row
                        // vanished between read and plan, treat as a
                        // concurrency-style failure so the caller retries.
                        return InventoryErrors.InsufficientAvailableStock(0, allocation.Quantity.Value);
                    }

                    plan.Add((allocInventory, allocation.Quantity, allocation.LotId, item.ProductId, item.LocationId));
                }

                continue;
            }

            // Non-FEFO branch: explicit LotId, or a product with no lots.
            var inventory = inventories.FirstOrDefault(i =>
                i.ProductId == item.ProductId
                && i.LocationId == item.LocationId
                && i.LotId == item.LotId);

            if (inventory is null)
                return InventoryErrors.InsufficientAvailableStock(0, item.Quantity);

            plan.Add((inventory, new Quantity(item.Quantity), item.LotId, item.ProductId, item.LocationId));
        }

        // Validation pass — the same inventory row may appear multiple
        // times across plan entries (e.g. two requested lines from the same
        // product+location+lot). Sum per row and check the total against
        // Available so we fail fast and don't half-reserve.
        var totalsByInventory = plan
            .GroupBy(p => p.Inventory)
            .Select(g => new { Inventory = g.Key, Total = g.Sum(x => x.Quantity.Value) });

        foreach (var row in totalsByInventory)
        {
            if (row.Total > row.Inventory.Available.Value)
                return InventoryErrors.InsufficientAvailableStock(
                    row.Inventory.Available.Value,
                    row.Total);
        }

        // Pass 2 — apply. By this point the plan is guaranteed to succeed
        // for every line; Reserve cannot fail because we already checked
        // Available above.
        var stockOut = new StockOut(Guid.NewGuid());

        foreach (var entry in plan)
        {
            var reserveResult = entry.Inventory.Reserve(entry.Quantity);
            if (reserveResult.IsFailure)
                return Result.Failure<Guid>(reserveResult.Error);

            stockOut.AddItem(entry.ProductId, entry.LocationId, entry.LotId, entry.Quantity);
        }

        await context.StockOuts.AddAsync(stockOut, cancellationToken);

        var saveResult = await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
        if (saveResult.IsFailure)
            return Result.Failure<Guid>(saveResult.Error);

        return stockOut.Id;
    }
}
