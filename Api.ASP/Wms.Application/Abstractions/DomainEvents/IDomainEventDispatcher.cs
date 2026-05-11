using Wms.Domain.Primitives;

namespace Wms.Application.Abstractions.DomainEvents;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
