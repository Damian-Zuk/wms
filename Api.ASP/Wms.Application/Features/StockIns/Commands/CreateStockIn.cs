using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Putaway;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Commands;

/// <summary>One requested receipt line. The system plans the locations — the caller does not choose them.</summary>
public sealed record StockInLineRequest(Guid ProductId, Guid? LotId, int Quantity);

public sealed record CreateStockInCommand(List<StockInLineRequest> Lines) : ICommand<Guid>;

public sealed class CreateStockInValidator : AbstractValidator<CreateStockInCommand>
{
    public CreateStockInValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
            line.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
    }
}

public sealed class CreateStockInCommandHandler(IAppDbContext context, IPutawayPlanner planner)
    : ICommandHandler<CreateStockInCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateStockInCommand request, CancellationToken cancellationToken)
    {
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var lotIds = request.Lines.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();

        // Products are needed (with preferred locations) for the FixedLocation strategy.
        var products = await context.Products
            .Include(p => p.PreferredLocations)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missingProduct = productIds.FirstOrDefault(id => !products.ContainsKey(id));
        if (missingProduct != default)
            return StockInErrors.ProductNotFound(missingProduct);

        var lots = lotIds.Count > 0
            ? await context.Lots.Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken)
            : [];

        var missingLot = lotIds.FirstOrDefault(id => !lots.ContainsKey(id));
        if (missingLot != default)
            return StockInErrors.LotNotFound(missingLot);

        // A single snapshot the planner reasons over. Suggestions are advisory: capacity
        // is only reserved at StartReceiving, but we factor in other stock-ins' active
        // reservations so the plan is as feasible as possible.
        var locations = await context.Locations.AsNoTracking().ToListAsync(cancellationToken);
        var inventories = await context.Inventories.AsNoTracking().ToListAsync(cancellationToken);
        var activeReservations = await context.CapacityReservations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var planContext = new PutawayPlanContext(locations, inventories, activeReservations);

        // Plan every line first; if any line can't be placed in full, fail before saving anything.
        var plannedLines = new List<(StockInLineRequest Line, IReadOnlyList<PlacementAllocation> Allocations)>();
        foreach (var line in request.Lines)
        {
            var product = products[line.ProductId];
            Lot? lot = line.LotId.HasValue ? lots[line.LotId.Value] : null;

            var plan = planner.Plan(product, lot, new Quantity(line.Quantity), planContext);
            if (plan.IsFailure)
                return Result.Failure<Guid>(plan.Error);

            plannedLines.Add((line, plan.Value));
        }

        var stockIn = new StockIn(Guid.NewGuid());
        foreach (var (line, allocations) in plannedLines)
        {
            var result = stockIn.AddLineWithPlacements(
                line.ProductId,
                line.LotId,
                new Quantity(line.Quantity),
                allocations.Select(a => (a.LocationId, a.Quantity, a.Strategy)));

            if (result.IsFailure)
                return Result.Failure<Guid>(result.Error);
        }

        await context.StockIns.AddAsync(stockIn, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return stockIn.Id;
    }
}
