using Wms.Application.Common.Data;
using Wms.Application.Common.Events;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Features.StockMovements.Events;

internal sealed class StockOutItemReturnedToStockDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<StockOutItemReturnedToStockDomainEvent>
{
    public async Task Handle(StockOutItemReturnedToStockDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var movement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.LocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.In,
            StockMovementSource.StockOutCancellation,
            domainEvent.StockOutId);

        await context.StockMovements.AddAsync(movement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
