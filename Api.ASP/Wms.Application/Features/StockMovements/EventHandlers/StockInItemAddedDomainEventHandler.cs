using Wms.Application.Abstractions.DomainEvents;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Features.StockMovements.EventHandlers;

internal sealed class StockInItemAddedDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<StockInItemAddedDomainEvent>
{
    public async Task Handle(StockInItemAddedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var movement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.LocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.In,
            StockMovementSource.StockIn,
            domainEvent.StockInId);

        await context.StockMovements.AddAsync(movement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
