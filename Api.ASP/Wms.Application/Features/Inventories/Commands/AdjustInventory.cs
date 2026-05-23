using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Extensions;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.Inventories.Commands;

public sealed record AdjustInventoryCommand(Guid Id, int QuantityChange, string? Reason) : ICommand;

public sealed class AdjustInventoryValidator : AbstractValidator<AdjustInventoryCommand>
{
    public AdjustInventoryValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Inventory ID is required");
        RuleFor(x => x.QuantityChange).NotEqual(0).WithMessage("Quantity change must not be zero");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Reason must be 500 characters or fewer");
    }
}

public sealed class AdjustInventoryCommandHandler(IAppDbContext context)
    : ICommandHandler<AdjustInventoryCommand>
{
    public async Task<Result> Handle(AdjustInventoryCommand command, CancellationToken cancellationToken)
    {
        var inventory = await context.Inventories
            .FirstOrDefaultAsync(i => i.Id == command.Id, cancellationToken);

        if (inventory is null)
            return InventoryErrors.NotFound(command.Id);

        var result = inventory.Adjust(command.QuantityChange, command.Reason);
        if (result.IsFailure)
            return result;

        return await context.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
    }
}
