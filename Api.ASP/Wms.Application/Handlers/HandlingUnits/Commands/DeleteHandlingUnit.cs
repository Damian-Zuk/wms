using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.HandlingUnits.Commands;

public sealed record DeleteHandlingUnitCommand(Guid Id) : ICommand;

public sealed class DeleteHandlingUnitValidator : AbstractValidator<DeleteHandlingUnitCommand>
{
    public DeleteHandlingUnitValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Handling unit ID is required");
    }
}

/// <summary>
/// Soft-deletes an empty handling unit. Two guards: the unit must hold no stock,
/// and no active stock-in/stock-out may still reference it — their cancel flows
/// would otherwise resurrect stock onto a deleted unit.
/// </summary>
public sealed class DeleteHandlingUnitCommandHandler(IAppDbContext context)
    : ICommandHandler<DeleteHandlingUnitCommand>
{
    public async Task<Result> Handle(DeleteHandlingUnitCommand command, CancellationToken cancellationToken)
    {
        var handlingUnit = await context.HandlingUnits
            .FirstOrDefaultAsync(h => h.Id == command.Id, cancellationToken);

        if (handlingUnit is null)
            return HandlingUnitErrors.NotFound(command.Id);

        var holdsStock = await context.Inventories
            .AnyAsync(
                i => i.HandlingUnitId == command.Id && (i.OnHand.Value > 0 || i.Reserved.Value > 0),
                cancellationToken);

        if (holdsStock)
            return HandlingUnitErrors.NotEmpty(command.Id);

        var referencedByActiveStockIn = await context.StockIns
            .AnyAsync(
                s => (s.Status == StockInStatus.Draft || s.Status == StockInStatus.Putaway)
                    && s.Lines.Any(l => l.Items.Any(i => i.HandlingUnitId == command.Id)),
                cancellationToken);

        var referencedByActiveStockOut = await context.StockOuts
            .AnyAsync(
                s => (s.Status == StockOutStatus.Draft || s.Status == StockOutStatus.Picking)
                    && s.Lines.Any(l => l.Items.Any(i => i.HandlingUnitId == command.Id)),
                cancellationToken);

        if (referencedByActiveStockIn || referencedByActiveStockOut)
            return HandlingUnitErrors.InUseByActiveDocuments(command.Id);

        handlingUnit.MarkAsDeleted();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
