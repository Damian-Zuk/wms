using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Auth;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.Services;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record ModifyPlacementRequest(Guid LocationId, int Quantity, Guid? HandlingUnitId = null);

/// <summary>Request body for re-planning a line's placements (route supplies the ids).</summary>
public sealed record ModifyStockInLinePlacementsRequest(List<ModifyPlacementRequest> Placements);

public sealed record ModifyStockInLinePlacementsCommand(
    Guid StockInId,
    Guid LineId,
    List<ModifyPlacementRequest> Placements) : ICommand;

public sealed class ModifyStockInLinePlacementsValidator : AbstractValidator<ModifyStockInLinePlacementsCommand>
{
    public ModifyStockInLinePlacementsValidator()
    {
        RuleFor(x => x.StockInId).NotEmpty().WithMessage("StockIn ID is required");
        RuleFor(x => x.LineId).NotEmpty().WithMessage("Line ID is required");
        RuleFor(x => x.Placements).NotEmpty().WithMessage("At least one placement is required");
        RuleForEach(x => x.Placements).ChildRules(placement =>
        {
            placement.RuleFor(x => x.LocationId).NotEmpty().WithMessage("Location ID is required");
            placement.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
    }
}

public sealed class ModifyStockInLinePlacementsCommandHandler(
    IAppDbContext context,
    ICurrentUserService currentUser)
    : ICommandHandler<ModifyStockInLinePlacementsCommand>
{
    public async Task<Result> Handle(ModifyStockInLinePlacementsCommand command, CancellationToken cancellationToken)
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

        // Fail fast on a total mismatch (the domain re-checks this authoritatively).
        var requestedTotal = command.Placements.Sum(p => p.Quantity);
        if (requestedTotal != line.Quantity.Value)
            return StockInErrors.PlacementsDoNotMatchLineTotal(line.ProductId, line.Quantity.Value, requestedTotal);

        // Handling units were declared at creation, so the request must keep exactly one
        // placement per unit with its declared quantity — only the location may change.
        // Loose placements can be reshaped freely.
        var currentHuQuantities = line.Items
            .Where(i => i.HandlingUnitId.HasValue)
            .ToDictionary(i => i.HandlingUnitId!.Value, i => i.Quantity.Value);

        var requestedHuPlacements = command.Placements
            .Where(p => p.HandlingUnitId.HasValue)
            .ToList();

        var unitsPreserved =
            requestedHuPlacements.Count == currentHuQuantities.Count
            && requestedHuPlacements
                .GroupBy(p => p.HandlingUnitId!.Value)
                .All(g => g.Count() == 1
                    && currentHuQuantities.TryGetValue(g.Key, out var declaredQuantity)
                    && g.Single().Quantity == declaredQuantity);

        if (!unitsPreserved)
            return HandlingUnitErrors.PlacementsMustPreserveHandlingUnits(line.Id);

        var locationIds = command.Placements.Select(p => p.LocationId).Distinct().ToList();

        var locations = await context.Locations
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var missingLocation = locationIds.FirstOrDefault(id => !locations.ContainsKey(id));
        if (missingLocation != default)
            return StockInErrors.LocationNotFound(missingLocation);

        var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, cancellationToken);
        if (product is null)
            return StockInErrors.ProductNotFound(line.ProductId);

        Lot? lot = null;
        if (line.LotId.HasValue)
        {
            lot = await context.Lots.FirstOrDefaultAsync(l => l.Id == line.LotId.Value, cancellationToken);
            if (lot is null)
                return StockInErrors.LotNotFound(line.LotId.Value);
        }

        var contentsByLocation = (await context.Inventories
                .Where(i => locationIds.Contains(i.LocationId))
                .ToListAsync(cancellationToken))
            .GroupBy(i => i.LocationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Other stock-ins' active reservations count against capacity; this draft holds none yet.
        var otherReservations = await context.CapacityReservations
            .Where(r =>
                locationIds.Contains(r.LocationId) &&
                r.StockInId != stockIn.Id)
            .ToListAsync(cancellationToken);

        // Every product occupying these locations (existing contents + other stock-ins'
        // reservations + this line), needed to weigh load on the Weight/Volume dimensions.
        var productIds = contentsByLocation.Values.SelectMany(c => c).Select(i => i.ProductId)
            .Concat(otherReservations.Select(r => r.ProductId))
            .Append(line.ProductId)
            .Distinct()
            .ToList();
        var products = await context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var occupancyByLocation = new Dictionary<Guid, CapacityOccupancy>();
        foreach (var reservation in otherReservations)
            if (products.TryGetValue(reservation.ProductId, out var reservationProduct))
                OccupancyFor(occupancyByLocation, reservation.LocationId)
                    .Add(CapacityLoadCalculator.Load(reservationProduct, reservation.Quantity));

        // Validate each target accepts its share, accumulating sibling placements as we go.
        foreach (var placement in command.Placements)
        {
            var location = locations[placement.LocationId];
            var contents = contentsByLocation.TryGetValue(placement.LocationId, out var c) ? c : [];
            var occupancy = OccupancyFor(occupancyByLocation, placement.LocationId);
            var quantity = new Quantity(placement.Quantity);

            var canAccept = location.CanAccept(product, lot, quantity, contents, occupancy, products);
            if (canAccept.IsFailure)
                return canAccept;

            occupancy.Add(CapacityLoadCalculator.Load(product, quantity));
        }

        var result = stockIn.ModifyLinePlacements(
            command.LineId,
            command.Placements.Select(p => (p.LocationId, p.Quantity, p.HandlingUnitId)),
            currentUser.UserName,
            DateTime.UtcNow);

        if (result.IsFailure)
            return result;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static CapacityOccupancy OccupancyFor(Dictionary<Guid, CapacityOccupancy> map, Guid locationId)
    {
        if (!map.TryGetValue(locationId, out var occupancy))
        {
            occupancy = new CapacityOccupancy();
            map[locationId] = occupancy;
        }

        return occupancy;
    }
}
