using Wms.Application.Common.Data;
using Wms.Application.Common.Events;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Handlers.StockMovements.Events;

internal sealed class StockInItemRemovedFromStockDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<StockInItemRemovedFromStockDomainEvent>
{
    public async Task Handle(StockInItemRemovedFromStockDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var movement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.LocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.Out,
            StockMovementSource.StockInCancellation,
            domainEvent.StockInId,
            domainEvent.HandlingUnitId);

        await context.StockMovements.AddAsync(movement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
