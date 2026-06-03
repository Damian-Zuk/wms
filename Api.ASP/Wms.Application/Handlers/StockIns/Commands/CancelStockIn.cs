using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record CancelStockInCommand(Guid Id) : ICommand;

public sealed class CancelStockInCommandHandler(IAppDbContext context)
    : ICommandHandler<CancelStockInCommand>
{
    public async Task<Result> Handle(CancelStockInCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.Id);

        var result = stockIn.Cancel();
        if (result.IsFailure)
            return result;

        // Release the remaining capacity holds. Anything already put away is on hand
        // and stays there; only the not-yet-placed holds are freed. A Draft cancel has
        // no holds at all.
        var reservations = await context.CapacityReservations
            .Where(r => r.StockInId == stockIn.Id)
            .ToListAsync(cancellationToken);

        context.CapacityReservations.RemoveRange(reservations);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
