using Wms.Domain.Primitives;

namespace Wms.Application.Common.Events;

public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task Handle(T domainEvent, CancellationToken cancellationToken);
}
