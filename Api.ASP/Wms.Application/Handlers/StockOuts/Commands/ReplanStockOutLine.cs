using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Application.Picking;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

/// <summary>Request body for re-planning a line's picks (route supplies the ids).</summary>
public sealed record ReplanStockOutLineRequest(PickingStrategyType Strategy);

public sealed record ReplanStockOutLineCommand(
    Guid StockOutId,
    Guid LineId,
    PickingStrategyType Strategy) : ICommand;

public sealed class ReplanStockOutLineValidator : AbstractValidator<ReplanStockOutLineCommand>
{
    public ReplanStockOutLineValidator()
    {
        RuleFor(x => x.StockOutId).NotEmpty().WithMessage("StockOut ID is required");
        RuleFor(x => x.LineId).NotEmpty().WithMessage("Line ID is required");
        RuleFor(x => x.Strategy).IsInEnum().WithMessage("A valid picking strategy is required");
    }
}

/// <summary>
/// Re-runs the picking planner for a single draft line with the chosen strategy and
/// replaces its allocations with the freshly computed plan (Draft only). The line's
/// current reservations (made by CreateStockOut) are released first so the planner's
/// snapshot sees those units as available again, the plan is computed, then the new
/// allocations are reserved against their sources. The line is stamped with the chosen
/// strategy (which lets a hand-edited, Manual line return to an automatic plan).
/// </summary>
public sealed class ReplanStockOutLineCommandHandler(IAppDbContext context, IPickingPlanner planner)
    : ICommandHandler<ReplanStockOutLineCommand>
{
    public async Task<Result> Handle(ReplanStockOutLineCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == command.StockOutId, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.StockOutId);

        if (stockOut.Status != StockOutStatus.Draft)
            return StockOutErrors.CannotModifyItems(stockOut.Status);

        var line = stockOut.Lines.FirstOrDefault(l => l.Id == command.LineId);
        if (line is null)
            return StockOutErrors.LineNotFound(command.LineId);

        // Every inventory source for this product (tracked — we move reservations on save).
        var inventories = await context.Inventories
            .Where(i => i.ProductId == line.ProductId)
            .ToListAsync(cancellationToken);

        // Release the line's current reservations before planning so the snapshot the
        // planner reasons over counts the units this draft already holds as available.
        // A draft never has picked units, so each item's full quantity is still reserved.
        var currentReservations = line.Items
            .Select(i => (i.LocationId, i.LotId, i.HandlingUnitId, i.Quantity.Value))
            .ToList();

        foreach (var (locationId, lotId, handlingUnitId, quantity) in currentReservations)
        {
            var inventory = inventories.FirstOrDefault(i =>
                i.LocationId == locationId && i.LotId == lotId && i.HandlingUnitId == handlingUnitId);
            if (inventory is null)
                continue;

            var release = inventory.ReleaseReservation(new Quantity(quantity));
            if (release.IsFailure)
                return release.Error;
        }

        var lots = await context.Lots
            .AsNoTracking()
            .Where(l => l.ProductId == line.ProductId)
            .ToListAsync(cancellationToken);

        var locations = await context.Locations.AsNoTracking().ToListAsync(cancellationToken);

        var pickContext = new PickingContext(inventories, lots, locations);

        var plan = planner.Plan(line.ProductId, command.Strategy, line.Quantity, pickContext);
        if (plan.IsFailure)
            return plan.Error;

        // Re-shape the document with the fresh plan (re-checks the sum; stamps the strategy).
        var replan = stockOut.ReplanLineAllocations(command.LineId, command.Strategy, plan.Value);
        if (replan.IsFailure)
            return replan.Error;

        // Reserve every planned allocation against its source. The planner already
        // committed against the (released) snapshot, so these never exceed Available;
        // the check is a fail-fast guard that keeps us from half-reserving on a shortfall.
        foreach (var allocation in plan.Value)
        {
            var inventory = inventories.FirstOrDefault(i =>
                i.LocationId == allocation.LocationId
                && i.LotId == allocation.LotId
                && i.HandlingUnitId == allocation.HandlingUnitId);

            if (inventory is null)
                return InventoryErrors.InsufficientAvailableStock(0, allocation.Quantity);

            var reserve = inventory.Reserve(new Quantity(allocation.Quantity));
            if (reserve.IsFailure)
                return reserve.Error;
        }

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
