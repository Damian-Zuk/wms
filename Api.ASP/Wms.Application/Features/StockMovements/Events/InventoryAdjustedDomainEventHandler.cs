using Wms.Application.Common.Data;
using Wms.Application.Common.Events;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Features.StockMovements.Events;

internal sealed class InventoryAdjustedDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<InventoryAdjustedDomainEvent>
{
    public async Task Handle(InventoryAdjustedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var type = domainEvent.QuantityChange > 0
            ? StockMovementType.In
            : StockMovementType.Out;

        var movement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.LocationId,
            domainEvent.LotId,
            Math.Abs(domainEvent.QuantityChange),
            type,
            StockMovementSource.Adjustment,
            domainEvent.InventoryId);

        await context.StockMovements.AddAsync(movement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
