using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Putaway;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.Models;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record ReplanStockInLineCommand(Guid StockInId, Guid LineId) : ICommand;

public sealed class ReplanStockInLineValidator : AbstractValidator<ReplanStockInLineCommand>
{
    public ReplanStockInLineValidator()
    {
        RuleFor(x => x.StockInId).NotEmpty().WithMessage("StockIn ID is required");
        RuleFor(x => x.LineId).NotEmpty().WithMessage("Line ID is required");
    }
}

/// <summary>
/// Re-runs the putaway planner for a single draft line and replaces its placements with
/// the freshly computed plan (Draft only). The plan is built against the same snapshot
/// CreateStockIn uses — every location, its on-hand inventory and the capacity already
/// held by other stock-ins' active reservations. A draft holds no reservations itself,
/// so the line's current (advisory) placements never count against the new plan.
/// </summary>
public sealed class ReplanStockInLineCommandHandler(IAppDbContext context, IPutawayPlanner planner)
    : ICommandHandler<ReplanStockInLineCommand>
{
    public async Task<Result> Handle(ReplanStockInLineCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .FirstOrDefaultAsync(s => s.Id == command.StockInId, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.StockInId);

        var line = stockIn.Lines.FirstOrDefault(l => l.Id == command.LineId);
        if (line is null)
            return StockInErrors.LineNotFound(command.LineId);

        // All products, with preferred locations: the line needs its own for the
        // PreferredLocation strategy, and the plan context needs every product that
        // occupies a candidate location to weigh its load on the Weight/Volume dimensions.
        var products = await context.Products
            .Include(p => p.PreferredLocations)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (!products.TryGetValue(line.ProductId, out var product))
            return StockInErrors.ProductNotFound(line.ProductId);

        Lot? lot = null;
        if (line.LotId.HasValue)
        {
            lot = await context.Lots.FirstOrDefaultAsync(l => l.Id == line.LotId.Value, cancellationToken);
            if (lot is null)
                return StockInErrors.LotNotFound(line.LotId.Value);
        }

        var locations = await context.Locations.AsNoTracking().ToListAsync(cancellationToken);
        var inventories = await context.Inventories.AsNoTracking().ToListAsync(cancellationToken);
        var activeReservations = await context.CapacityReservations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var planContext = new PutawayPlanContext(locations, inventories, activeReservations, products.Values);

        // The line's handling units keep their declared quantities — each chunk is
        // re-planned to a (possibly new) single location — and the loose remainder
        // is re-planned as before.
        var allocations = new List<PlacementAllocation>();
        foreach (var item in line.Items.Where(i => i.HandlingUnitId.HasValue))
        {
            var chunk = planner.PlanSingle(product, lot, item.Quantity, planContext);
            if (chunk.IsFailure)
                return chunk.Error;

            allocations.Add(chunk.Value with { HandlingUnitId = item.HandlingUnitId });
        }

        var looseQuantity = line.Quantity.Value - allocations.Sum(a => a.Quantity);
        if (looseQuantity > 0)
        {
            var plan = planner.Plan(product, lot, new Quantity(looseQuantity), planContext);
            if (plan.IsFailure)
                return plan.Error;

            allocations.AddRange(plan.Value);
        }

        var replan = stockIn.ReplanLinePlacements(command.LineId, allocations);
        if (replan.IsFailure)
            return replan.Error;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
