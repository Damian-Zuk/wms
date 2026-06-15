using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.Services;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.HandlingUnits.Commands;

/// <summary>Request body for moving a handling unit (route supplies the id).</summary>
public sealed record MoveHandlingUnitRequest(Guid DestinationLocationId);

public sealed record MoveHandlingUnitCommand(Guid HandlingUnitId, Guid DestinationLocationId) : ICommand;

public sealed class MoveHandlingUnitValidator : AbstractValidator<MoveHandlingUnitCommand>
{
    public MoveHandlingUnitValidator()
    {
        RuleFor(x => x.HandlingUnitId).NotEmpty().WithMessage("Handling unit ID is required");
        RuleFor(x => x.DestinationLocationId).NotEmpty().WithMessage("Destination location ID is required");
    }
}

/// <summary>
/// Relocates a handling unit with everything on it in one operation — the point of
/// palletised stock. Every inventory row of the unit moves along (FIFO age travels);
/// a single reservation anywhere on the unit blocks the move, because the pick that
/// owns it points at the row in its current location. The destination must accept
/// each row like any other placement (zone, mixing rules, capacity).
/// </summary>
public sealed class MoveHandlingUnitCommandHandler(IAppDbContext context)
    : ICommandHandler<MoveHandlingUnitCommand>
{
    public async Task<Result> Handle(MoveHandlingUnitCommand command, CancellationToken cancellationToken)
    {
        var handlingUnit = await context.HandlingUnits
            .FirstOrDefaultAsync(h => h.Id == command.HandlingUnitId, cancellationToken);

        if (handlingUnit is null)
            return HandlingUnitErrors.NotFound(command.HandlingUnitId);

        if (!handlingUnit.IsPlaced)
            return HandlingUnitErrors.NotPlaced(handlingUnit.Id);

        if (handlingUnit.LocationId == command.DestinationLocationId)
            return HandlingUnitErrors.SameLocation();

        var destination = await context.Locations
            .FirstOrDefaultAsync(l => l.Id == command.DestinationLocationId, cancellationToken);

        if (destination is null)
            return HandlingUnitErrors.LocationNotFound(command.DestinationLocationId);

        var contents = await context.Inventories
            .Where(i => i.HandlingUnitId == handlingUnit.Id)
            .ToListAsync(cancellationToken);

        if (contents.Any(i => i.Reserved.Value > 0))
            return HandlingUnitErrors.HasReservedStock(handlingUnit.Id);

        var movingRows = contents.Where(i => i.OnHand.Value > 0).ToList();

        // Validate the destination accepts each row, accumulating the rows already
        // admitted so the whole unit is checked, not each row in isolation.
        if (movingRows.Count > 0)
        {
            var destinationContents = await context.Inventories
                .Where(i => i.LocationId == command.DestinationLocationId)
                .ToListAsync(cancellationToken);

            var productIds = destinationContents.Select(i => i.ProductId)
                .Concat(movingRows.Select(i => i.ProductId))
                .Distinct()
                .ToList();
            var products = await context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var lotIds = movingRows.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();
            var lots = lotIds.Count > 0
                ? await context.Lots.Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken)
                : [];

            var occupancy = new CapacityOccupancy();
            foreach (var row in movingRows)
            {
                if (!products.TryGetValue(row.ProductId, out var product))
                    return HandlingUnitErrors.ProductNotFound(row.ProductId);

                var lot = row.LotId.HasValue ? lots.GetValueOrDefault(row.LotId.Value) : null;
                var quantity = new Quantity(row.OnHand.Value);

                var canAccept = destination.CanAccept(product, lot, quantity, destinationContents, occupancy, products);
                if (canAccept.IsFailure)
                    return canAccept;

                occupancy.Add(CapacityLoadCalculator.Load(product, quantity));
            }
        }

        // One move id ties the per-row movement pairs together in the audit trail.
        var moveId = Guid.NewGuid();
        foreach (var row in contents)
        {
            var relocate = row.RelocateWith(command.DestinationLocationId, moveId);
            if (relocate.IsFailure)
                return relocate;
        }

        var move = handlingUnit.MoveTo(command.DestinationLocationId);
        if (move.IsFailure)
            return move;

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
