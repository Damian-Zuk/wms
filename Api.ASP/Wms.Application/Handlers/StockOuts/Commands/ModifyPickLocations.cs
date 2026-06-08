using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Extensions;
using Wms.Domain.Errors;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

public sealed record PickAllocationRequest(Guid LocationId, Guid? LotId, int Quantity);

public sealed record ModifyPickLocationsRequest(List<PickAllocationRequest> Allocations);

public sealed record ModifyPickLocationsCommand(
    Guid StockOutId,
    Guid LineId,
    List<PickAllocationRequest> Allocations) : ICommand;

public sealed class ModifyPickLocationsValidator : AbstractValidator<ModifyPickLocationsCommand>
{
    public ModifyPickLocationsValidator()
    {
        RuleFor(x => x.StockOutId).NotEmpty().WithMessage("StockOut ID is required");
        RuleFor(x => x.LineId).NotEmpty().WithMessage("Line ID is required");
        RuleFor(x => x.Allocations).NotEmpty().WithMessage("At least one allocation is required");
        RuleForEach(x => x.Allocations).ChildRules(allocation =>
        {
            allocation.RuleFor(x => x.LocationId).NotEmpty().WithMessage("Location ID is required");
            allocation.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
    }
}

/// <summary>
/// Re-plans a draft stock-out line's pick allocations from a user-supplied set of
/// location/lot picks (Draft only). The reservations made by CreateStockOut are moved
/// to the new sources: the line's current reservations are released first, then the
/// requested allocations are reserved against their inventory rows (which fails fast if
/// a source can't cover its share). The line and its items are stamped Manual.
/// </summary>
public sealed class ModifyPickLocationsCommandHandler(IAppDbContext context)
    : ICommandHandler<ModifyPickLocationsCommand>
{
    public async Task<Result> Handle(ModifyPickLocationsCommand command, CancellationToken cancellationToken)
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

        // Fail fast on a total mismatch (the domain re-checks this authoritatively).
        var requestedTotal = command.Allocations.Sum(a => a.Quantity);
        if (requestedTotal != line.Quantity.Value)
            return StockOutErrors.AllocationsDoNotMatchLineTotal(line.ProductId, line.Quantity.Value, requestedTotal);

        var requestedLocationIds = command.Allocations.Select(a => a.LocationId).Distinct().ToList();
        var existingLocationIds = await context.Locations
            .AsNoTracking()
            .Where(l => requestedLocationIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var missingLocation = requestedLocationIds.Except(existingLocationIds).FirstOrDefault();
        if (missingLocation != default)
            return StockOutErrors.LocationNotFound(missingLocation);

        var requestedLotIds = command.Allocations
            .Where(a => a.LotId.HasValue)
            .Select(a => a.LotId!.Value)
            .Distinct()
            .ToList();

        if (requestedLotIds.Count > 0)
        {
            var existingLotIds = await context.Lots
                .AsNoTracking()
                .Where(l => requestedLotIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            var missingLot = requestedLotIds.Except(existingLotIds).FirstOrDefault();
            if (missingLot != default)
                return StockOutErrors.LotNotFound(missingLot);
        }

        // Every inventory row the move touches: the line's current sources (to release)
        // and the requested targets (to reserve), all for this line's product.
        var touchedLocationIds = requestedLocationIds
            .Concat(line.Items.Select(i => i.LocationId))
            .Distinct()
            .ToList();

        var inventories = await context.Inventories
            .Where(i => i.ProductId == line.ProductId && touchedLocationIds.Contains(i.LocationId))
            .ToListAsync(cancellationToken);

        // Snapshot the line's current reservations before the domain replaces its items.
        // A draft never has picked units, so each item's full quantity is still reserved.
        var currentReservations = line.Items
            .Select(i => (i.LocationId, i.LotId, i.Quantity.Value))
            .ToList();

        // Re-shape the document first (Draft-only; re-checks the sum; stamps Manual).
        var modify = stockOut.ModifyLineAllocations(
            command.LineId,
            command.Allocations.Select(a => (a.LocationId, a.LotId, a.Quantity)));
        
        if (modify.IsFailure)
            return modify.Error;

        // Release the old reservations first so kept sources free up before we re-reserve.
        foreach (var (locationId, lotId, quantity) in currentReservations)
        {
            var inventory = inventories.FirstOrDefault(i => i.LocationId == locationId && i.LotId == lotId);
            if (inventory is null)
                continue;

            var release = inventory.ReleaseReservation(new Quantity(quantity));
            if (release.IsFailure)
                return release.Error;
        }

        // Reserve every requested allocation against its source.
        foreach (var allocation in command.Allocations)
        {
            var inventory = inventories.FirstOrDefault(i =>
                i.LocationId == allocation.LocationId && i.LotId == allocation.LotId);

            if (inventory is null)
                return InventoryErrors.InsufficientAvailableStock(0, allocation.Quantity);

            var reserve = inventory.Reserve(new Quantity(allocation.Quantity));
            if (reserve.IsFailure)
                return reserve.Error;
        }

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
