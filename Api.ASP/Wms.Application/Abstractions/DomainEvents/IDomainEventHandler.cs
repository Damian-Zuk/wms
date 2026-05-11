using Wms.Domain.Primitives;

namespace Wms.Application.Abstractions.DomainEvents;

public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task Handle(T domainEvent, CancellationToken cancellationToken);
}
