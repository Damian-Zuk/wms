using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Commands;

public sealed record StartReceivingStockInCommand(Guid Id) : ICommand;

public sealed class StartReceivingStockInCommandHandler(IAppDbContext context)
    : ICommandHandler<StartReceivingStockInCommand>
{
    public async Task<Result> Handle(StartReceivingStockInCommand command, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(command.Id);

        var result = stockIn.StartReceiving();
        if (result.IsFailure)
            return result;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
