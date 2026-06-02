using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Commands;

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

        // Free any capacity reserved at StartReceiving (a Draft cancel has none).
        var reservations = await context.CapacityReservations
            .Where(r => r.StockInId == stockIn.Id)
            .ToListAsync(cancellationToken);

        context.CapacityReservations.RemoveRange(reservations);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
