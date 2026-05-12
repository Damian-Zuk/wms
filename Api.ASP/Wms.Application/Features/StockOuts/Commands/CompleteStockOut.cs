using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Commands;

public sealed record CompleteStockOutCommand(Guid Id) : ICommand;

public sealed class CompleteStockOutCommandHandler(IAppDbContext context)
    : ICommandHandler<CompleteStockOutCommand>
{
    public async Task<Result> Handle(CompleteStockOutCommand command, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(command.Id);

        var result = stockOut.Complete();
        if (result.IsFailure)
            return result;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
