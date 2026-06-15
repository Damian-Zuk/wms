using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Application.Picking;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Models;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

public sealed record StockOutLineRequest(Guid ProductId, PickingStrategyType Strategy, int Quantity);

public sealed record CreateStockOutCommand(List<StockOutLineRequest> Lines, string? Description) : ICommand<Guid>;

public sealed class CreateStockOutValidator : AbstractValidator<CreateStockOutCommand>
{
    public CreateStockOutValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
            line.RuleFor(x => x.Strategy).IsInEnum().WithMessage("A valid picking strategy is required");
            line.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description != null);
    }
}

public sealed class CreateStockOutCommandHandler(IAppDbContext context, IPickingPlanner planner)
    : ICommandHandler<CreateStockOutCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateStockOutCommand request, CancellationToken cancellationToken)
    {
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();

        var existingProductIds = await context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var missingProduct = productIds.Except(existingProductIds).FirstOrDefault();
        if (missingProduct != default)
            return StockOutErrors.ProductNotFound(missingProduct);

        // One snapshot the planner reasons over: tracked inventory (we reserve against
        // it on save), plus the lots and locations needed to rank pick candidates.
        var inventories = await context.Inventories
            .Where(i => productIds.Contains(i.ProductId))
            .ToListAsync(cancellationToken);

        var lots = await context.Lots
            .AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId))
            .ToListAsync(cancellationToken);

        var locations = await context.Locations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var pickContext = new PickingContext(inventories, lots, locations);

        // Plan every line first; if any line can't be picked in full, fail before saving.
        var plannedLines = new List<(StockOutLineRequest Line, IReadOnlyList<PickAllocation> Allocations)>();
        foreach (var line in request.Lines)
        {
            var plan = planner.Plan(line.ProductId, line.Strategy, new Quantity(line.Quantity), pickContext);
            if (plan.IsFailure)
                return plan.Error;

            plannedLines.Add((line, plan.Value));
        }

        // Resolve every allocation to its tracked inventory row and sum per row, so two
        // lines drawing from the same source are checked together. The planner already
        // committed against the snapshot, so these totals never exceed Available; the
        // check is a fail-fast guard that keeps us from half-reserving on a shortfall.
        var reservePlan = new List<(Inventory Inventory, int Quantity)>();
        foreach (var (line, allocations) in plannedLines)
        {
            foreach (var allocation in allocations)
            {
                var inventory = inventories.FirstOrDefault(i =>
                    i.ProductId == line.ProductId
                    && i.LocationId == allocation.LocationId
                    && i.LotId == allocation.LotId
                    && i.HandlingUnitId == allocation.HandlingUnitId);

                if (inventory is null)
                    return InventoryErrors.InsufficientAvailableStock(0, allocation.Quantity);

                reservePlan.Add((inventory, allocation.Quantity));
            }
        }

        foreach (var row in reservePlan.GroupBy(p => p.Inventory))
        {
            var total = row.Sum(x => x.Quantity);
            if (total > row.Key.Available.Value)
                return InventoryErrors.InsufficientAvailableStock(row.Key.Available.Value, total);
        }

        // Apply: build the aggregate, then reserve every allocation's units.
        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.SetDescription(request.Description);
        foreach (var (line, allocations) in plannedLines)
        {
            var add = stockOut.AddLineWithAllocations(
                line.ProductId,
                line.Strategy,
                new Quantity(line.Quantity),
                allocations);

            if (add.IsFailure)
                return add.Error;
        }

        foreach (var (inventory, quantity) in reservePlan)
        {
            var reserve = inventory.Reserve(new Quantity(quantity));
            if (reserve.IsFailure)
                return reserve.Error;
        }

        await context.StockOuts.AddAsync(stockOut, cancellationToken);

        var saveResult = await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return stockOut.Id;
    }
}
