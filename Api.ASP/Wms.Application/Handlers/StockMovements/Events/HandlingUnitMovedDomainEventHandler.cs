using Wms.Application.Common.Data;
using Wms.Application.Common.Events;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Handlers.StockMovements.Events;

internal sealed class HandlingUnitMovedDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<HandlingUnitMovedDomainEvent>
{
    public async Task Handle(HandlingUnitMovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var outMovement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.SourceLocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.Out,
            StockMovementSource.HandlingUnitMove,
            domainEvent.MoveId,
            domainEvent.HandlingUnitId);

        var inMovement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.DestinationLocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.In,
            StockMovementSource.HandlingUnitMove,
            domainEvent.MoveId,
            domainEvent.HandlingUnitId);

        await context.StockMovements.AddAsync(outMovement, cancellationToken);
        await context.StockMovements.AddAsync(inMovement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
