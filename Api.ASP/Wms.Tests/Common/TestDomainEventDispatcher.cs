using Wms.Application.Common.Events;
using Wms.Domain.Primitives;

namespace Wms.Tests.Common;

/// <summary>
/// In-memory dispatcher used by integration tests. Captures every event it
/// receives for assertions, and routes events to per-type handlers when the
/// test wires them up via <see cref="Register{TEvent}"/>.
/// </summary>
public sealed class TestDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly Dictionary<Type, List<Func<IDomainEvent, CancellationToken, Task>>> _handlers = new();

    public List<IDomainEvent> DispatchedEvents { get; } = new();

    public void Register<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDomainEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list))
        {
            list = new List<Func<IDomainEvent, CancellationToken, Task>>();
            _handlers[typeof(TEvent)] = list;
        }

        list.Add((e, ct) => handler((TEvent)e, ct));
    }

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var evt in domainEvents)
        {
            DispatchedEvents.Add(evt);

            if (_handlers.TryGetValue(evt.GetType(), out var list))
            {
                foreach (var handler in list)
                {
                    await handler(evt, cancellationToken);
                }
            }
        }
    }
}
