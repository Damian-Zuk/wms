using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Errors;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.HandlingUnits.Commands;

/// <summary>Request body for unpacking stock off a handling unit (route supplies the id).</summary>
public sealed record UnpackHandlingUnitRequest(Guid ProductId, Guid? LotId, int Quantity);

public sealed record UnpackHandlingUnitCommand(
    Guid HandlingUnitId,
    Guid ProductId,
    Guid? LotId,
    int Quantity) : ICommand;

public sealed class UnpackHandlingUnitValidator : AbstractValidator<UnpackHandlingUnitCommand>
{
    public UnpackHandlingUnitValidator()
    {
        RuleFor(x => x.HandlingUnitId).NotEmpty().WithMessage("Handling unit ID is required");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

/// <summary>
/// Takes stock off a handling unit back to loose stock at the same location — the
/// mirror of packing. Only Available (unreserved) stock may be unpacked, because a
/// reservation pins the exact row the pick will draw from.
/// </summary>
public sealed class UnpackHandlingUnitCommandHandler(IAppDbContext context)
    : ICommandHandler<UnpackHandlingUnitCommand>
{
    public async Task<Result> Handle(UnpackHandlingUnitCommand command, CancellationToken cancellationToken)
    {
        var handlingUnit = await context.HandlingUnits
            .FirstOrDefaultAsync(h => h.Id == command.HandlingUnitId, cancellationToken);

        if (handlingUnit is null)
            return HandlingUnitErrors.NotFound(command.HandlingUnitId);

        if (!handlingUnit.IsPlaced)
            return HandlingUnitErrors.NotPlaced(handlingUnit.Id);

        var locationId = handlingUnit.LocationId!.Value;

        var unitRow = await context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == command.ProductId
                    && i.LocationId == locationId
                    && i.LotId == command.LotId
                    && i.HandlingUnitId == handlingUnit.Id,
                cancellationToken);

        var quantity = new Quantity(command.Quantity);

        if (unitRow is null || quantity.Value > unitRow.Available.Value)
            return InventoryErrors.InsufficientAvailableStock(unitRow?.Available.Value ?? 0, command.Quantity);

        var looseRow = await context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == command.ProductId
                    && i.LocationId == locationId
                    && i.LotId == command.LotId
                    && i.HandlingUnitId == null,
                cancellationToken);

        if (looseRow is null)
        {
            looseRow = new Inventory(command.ProductId, locationId, command.LotId);
            await context.Inventories.AddAsync(looseRow, cancellationToken);
        }

        var rebucket = unitRow.Rebucket(looseRow, quantity);
        if (rebucket.IsFailure)
            return rebucket;

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
