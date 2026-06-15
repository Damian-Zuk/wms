using Wms.Application.Common.Data;
using Wms.Application.Common.Events;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Handlers.StockMovements.Events;

internal sealed class StockInItemPutawayDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<StockInItemPutawayDomainEvent>
{
    public async Task Handle(StockInItemPutawayDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var movement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.LocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.In,
            StockMovementSource.StockIn,
            domainEvent.StockInId,
            domainEvent.HandlingUnitId);

        await context.StockMovements.AddAsync(movement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
