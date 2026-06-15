using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.HandlingUnits;
using Wms.Application.Putaway;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Models;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

/// <summary>
/// A handling unit announced on a receipt line (ASN style): how many of the line's
/// units arrive on it. <paramref name="Code"/> is optional — left null, a license
/// plate is generated.
/// </summary>
public sealed record DeclaredHandlingUnitRequest(int Quantity, HandlingUnitType Type, string? Code = null);

/// <summary>
/// One requested receipt line. The system plans the locations — the caller does not
/// choose them. Declared handling units must not exceed the line quantity; any
/// remainder is received as loose stock.
/// </summary>
public sealed record StockInLineRequest(
    Guid ProductId,
    Guid? LotId,
    int Quantity,
    List<DeclaredHandlingUnitRequest>? HandlingUnits = null);

public sealed record CreateStockInCommand(List<StockInLineRequest> Lines, string? Description) : ICommand<Guid>;

public sealed class CreateStockInValidator : AbstractValidator<CreateStockInCommand>
{
    public CreateStockInValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
            line.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
            line.RuleFor(x => x)
                .Must(l => l.HandlingUnits is null || l.HandlingUnits.Sum(h => h.Quantity) <= l.Quantity)
                .WithMessage("Declared handling unit quantities must not exceed the line quantity");
            line.RuleForEach(x => x.HandlingUnits).ChildRules(hu =>
            {
                hu.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Handling unit quantity must be greater than 0");
                hu.RuleFor(x => x.Type).IsInEnum().WithMessage("Handling unit type is invalid");
                hu.RuleFor(x => x.Code).MaximumLength(50).When(x => x.Code != null)
                    .WithMessage("Handling unit code must not exceed 50 characters");
            });
        });
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description != null);
    }
}

public sealed class CreateStockInCommandHandler(
    IAppDbContext context,
    IPutawayPlanner planner,
    IHandlingUnitCodeGenerator codeGenerator)
    : ICommandHandler<CreateStockInCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateStockInCommand request, CancellationToken cancellationToken)
    {
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var lotIds = request.Lines.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();

        // All products, with preferred locations: the requested lines need theirs for the
        // PreferredLocation strategy, and the plan context needs every product that occupies
        // a candidate location to weigh its existing contents on the Weight/Volume dimensions.
        var products = await context.Products
            .Include(p => p.PreferredLocations)
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
        // is only reserved at StartPutaway, but we factor in other stock-ins' active
        // reservations so the plan is as feasible as possible.
        var locations = await context.Locations.AsNoTracking().ToListAsync(cancellationToken);
        var inventories = await context.Inventories.AsNoTracking().ToListAsync(cancellationToken);
        var activeReservations = await context.CapacityReservations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var planContext = new PutawayPlanContext(locations, inventories, activeReservations, products.Values);

        var codeCheck = await EnsureCodesAreFreeAsync(request, cancellationToken);
        if (codeCheck.IsFailure)
            return Result.Failure<Guid>(codeCheck.Error);

        // Plan every line first; if any line can't be placed in full, fail before saving
        // anything. Handling unit chunks go first — each must land whole in one location —
        // then the loose remainder fills whatever space is left. The declared units are
        // created unplaced (they get a location at putaway) and only persist when the
        // whole plan succeeds.
        var plannedLines = new List<(StockInLineRequest Line, IReadOnlyList<PlacementAllocation> Allocations)>();
        foreach (var line in request.Lines)
        {
            var product = products[line.ProductId];
            Lot? lot = line.LotId.HasValue ? lots[line.LotId.Value] : null;

            var allocations = new List<PlacementAllocation>();
            foreach (var declaration in line.HandlingUnits ?? [])
            {
                var code = declaration.Code ?? await codeGenerator.NextCodeAsync(cancellationToken);
                var unit = new HandlingUnit(new HandlingUnitCode(code), declaration.Type);
                await context.HandlingUnits.AddAsync(unit, cancellationToken);

                var chunk = planner.PlanSingle(product, lot, new Quantity(declaration.Quantity), planContext);
                if (chunk.IsFailure)
                    return Result.Failure<Guid>(chunk.Error);

                allocations.Add(chunk.Value with { HandlingUnitId = unit.Id });
            }

            var looseQuantity = line.Quantity - (line.HandlingUnits?.Sum(h => h.Quantity) ?? 0);
            if (looseQuantity > 0)
            {
                var plan = planner.Plan(product, lot, new Quantity(looseQuantity), planContext);
                if (plan.IsFailure)
                    return Result.Failure<Guid>(plan.Error);

                allocations.AddRange(plan.Value);
            }

            plannedLines.Add((line, allocations));
        }

        var stockIn = new StockIn(Guid.NewGuid());
        stockIn.SetDescription(request.Description);
        foreach (var (line, allocations) in plannedLines)
        {
            var result = stockIn.AddLineWithPlacements(
                line.ProductId,
                line.LotId,
                new Quantity(line.Quantity),
                allocations);

            if (result.IsFailure)
                return Result.Failure<Guid>(result.Error);
        }

        await context.StockIns.AddAsync(stockIn, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return stockIn.Id;
    }

    /// <summary>
    /// Rejects caller-supplied codes that repeat within the request or already exist.
    /// The unique index on Code is the backstop for concurrent requests.
    /// </summary>
    private async Task<Result> EnsureCodesAreFreeAsync(CreateStockInCommand request, CancellationToken cancellationToken)
    {
        var manualCodes = request.Lines
            .SelectMany(l => l.HandlingUnits ?? [])
            .Where(h => h.Code is not null)
            .Select(h => h.Code!)
            .ToList();

        if (manualCodes.Count == 0)
            return Result.Success();

        var duplicate = manualCodes
            .GroupBy(c => c)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
            return HandlingUnitErrors.CodeAlreadyExists(duplicate.Key);

        var existing = await context.HandlingUnits
            .Where(h => manualCodes.Contains(h.Code.Value))
            .Select(h => h.Code.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return HandlingUnitErrors.CodeAlreadyExists(existing);

        return Result.Success();
    }
}
