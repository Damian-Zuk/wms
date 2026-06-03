using Wms.Application.Common.Messaging;
using Wms.Application.Putaway;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record StartReceivingStockInCommand(Guid Id) : ICommand;

public sealed class StartReceivingStockInCommandHandler(ICapacityReservationService reservationService)
    : ICommandHandler<StartReceivingStockInCommand>
{
    // Verifying capacity and reserving it must happen atomically under a row lock,
    // so the work lives in an Infrastructure service. This handler just delegates.
    public Task<Result> Handle(StartReceivingStockInCommand command, CancellationToken cancellationToken)
        => reservationService.ReserveForStartReceivingAsync(command.Id, cancellationToken);
}
