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

/// <summary>Request body for packing stock onto a handling unit (route supplies the id).</summary>
public sealed record PackHandlingUnitRequest(Guid ProductId, Guid? LotId, int Quantity);

public sealed record PackHandlingUnitCommand(
    Guid HandlingUnitId,
    Guid ProductId,
    Guid? LotId,
    int Quantity) : ICommand;

public sealed class PackHandlingUnitValidator : AbstractValidator<PackHandlingUnitCommand>
{
    public PackHandlingUnitValidator()
    {
        RuleFor(x => x.HandlingUnitId).NotEmpty().WithMessage("Handling unit ID is required");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

/// <summary>
/// Packs loose stock standing at the handling unit's location onto the unit. A pure
/// re-bucketing — the stock does not move and no movement rows are written; only
/// Available (unreserved) loose stock may be packed.
/// </summary>
public sealed class PackHandlingUnitCommandHandler(IAppDbContext context)
    : ICommandHandler<PackHandlingUnitCommand>
{
    public async Task<Result> Handle(PackHandlingUnitCommand command, CancellationToken cancellationToken)
    {
        var handlingUnit = await context.HandlingUnits
            .FirstOrDefaultAsync(h => h.Id == command.HandlingUnitId, cancellationToken);

        if (handlingUnit is null)
            return HandlingUnitErrors.NotFound(command.HandlingUnitId);

        if (!handlingUnit.IsPlaced)
            return HandlingUnitErrors.NotPlaced(handlingUnit.Id);

        var locationId = handlingUnit.LocationId!.Value;

        var looseRow = await context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == command.ProductId
                    && i.LocationId == locationId
                    && i.LotId == command.LotId
                    && i.HandlingUnitId == null,
                cancellationToken);

        if (looseRow is null)
            return HandlingUnitErrors.InsufficientLooseStock(command.ProductId, locationId, 0, command.Quantity);

        var quantity = new Quantity(command.Quantity);
        if (quantity.Value > looseRow.Available.Value)
            return HandlingUnitErrors.InsufficientLooseStock(
                command.ProductId, locationId, looseRow.Available.Value, command.Quantity);

        var unitRow = await context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == command.ProductId
                    && i.LocationId == locationId
                    && i.LotId == command.LotId
                    && i.HandlingUnitId == handlingUnit.Id,
                cancellationToken);

        if (unitRow is null)
        {
            unitRow = new Inventory(command.ProductId, locationId, command.LotId, handlingUnit.Id);
            await context.Inventories.AddAsync(unitRow, cancellationToken);
        }

        var rebucket = looseRow.Rebucket(unitRow, quantity);
        if (rebucket.IsFailure)
            return rebucket;

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
