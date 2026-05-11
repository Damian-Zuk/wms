using Wms.Application.Abstractions.DomainEvents;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;

namespace Wms.Application.Features.StockMovements.EventHandlers;

internal sealed class StockOutItemAddedDomainEventHandler(IAppDbContext context)
    : IDomainEventHandler<StockOutItemAddedDomainEvent>
{
    public async Task Handle(StockOutItemAddedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var movement = new StockMovement(
            domainEvent.ProductId,
            domainEvent.LocationId,
            domainEvent.LotId,
            domainEvent.Quantity,
            StockMovementType.Out,
            StockMovementSource.StockOut,
            domainEvent.StockOutId);

        await context.StockMovements.AddAsync(movement, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
