using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

public sealed record UpdateStockOutDescriptionRequest(string? Description);

public sealed record UpdateStockOutDescriptionCommand(Guid StockOutId, string? Description) : ICommand;

public sealed class UpdateStockOutDescriptionValidator : AbstractValidator<UpdateStockOutDescriptionCommand>
{
    public UpdateStockOutDescriptionValidator()
    {
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description != null);
    }
}

public sealed class UpdateStockOutDescriptionCommandHandler(IAppDbContext context)
    : ICommandHandler<UpdateStockOutDescriptionCommand>
{
    public async Task<Result> Handle(UpdateStockOutDescriptionCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .FirstOrDefaultAsync(s => s.Id == command.StockOutId, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.StockOutId);

        stockOut.SetDescription(command.Description);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
