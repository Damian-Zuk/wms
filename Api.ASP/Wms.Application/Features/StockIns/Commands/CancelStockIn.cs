using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
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

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
