using Wms.Application.Common.Messaging;
using Wms.Application.Putaway;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Commands;

public sealed record StartPutawayStockInCommand(Guid Id) : ICommand;

public sealed class StartPutawayStockInCommandHandler(ICapacityReservationService reservationService)
    : ICommandHandler<StartPutawayStockInCommand>
{
    // Verifying capacity and reserving it must happen atomically under a row lock,
    // so the work lives in an Infrastructure service. This handler just delegates.
    public Task<Result> Handle(StartPutawayStockInCommand command, CancellationToken cancellationToken)
        => reservationService.ReserveForStartPutawayAsync(command.Id, cancellationToken);
}
