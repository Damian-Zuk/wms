using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record UpdateStockInDescriptionRequest(string? Description);

public sealed record UpdateStockInDescriptionCommand(Guid StockInId, string? Description) : ICommand;

public sealed class UpdateStockInDescriptionValidator : AbstractValidator<UpdateStockInDescriptionCommand>
{
    public UpdateStockInDescriptionValidator()
    {
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description != null);
    }
}

public sealed class UpdateStockInDescriptionCommandHandler(IAppDbContext context)
    : ICommandHandler<UpdateStockInDescriptionCommand>
{
    public async Task<Result> Handle(UpdateStockInDescriptionCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .FirstOrDefaultAsync(s => s.Id == command.StockInId, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.StockInId);

        stockIn.SetDescription(command.Description);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
