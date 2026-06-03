using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Commands;

public sealed record StartPickingStockOutCommand(Guid Id) : ICommand;

/// <summary>
/// Transitions a Draft stock-out into Picking. The reservation against
/// inventory was already established by CreateStockOut, so this handler
/// touches nothing physical — it only flips the status. Physical removal
/// happens at Pack.
/// </summary>
public sealed class StartPickingStockOutCommandHandler(IAppDbContext context)
    : ICommandHandler<StartPickingStockOutCommand>
{
    public async Task<Result> Handle(StartPickingStockOutCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.Id);

        var transition = stockOut.StartPicking();
        if (transition.IsFailure)
            return transition;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
