using Wms.Application.Common.Data;
using Wms.Application.Common.Events;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Handlers.StockMovements.Events;

internal sealed class StockTransferredDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<StockTransferredDomainEvent>
{
    public async Task Handle(StockTransferredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var outMovement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.SourceLocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.Out,
            StockMovementSource.Transfer,
            domainEvent.TransferId,
            domainEvent.SourceHandlingUnitId);

        var inMovement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.DestinationLocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.In,
            StockMovementSource.Transfer,
            domainEvent.TransferId,
            domainEvent.DestinationHandlingUnitId);

        await context.StockMovements.AddAsync(outMovement, cancellationToken);
        await context.StockMovements.AddAsync(inMovement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
