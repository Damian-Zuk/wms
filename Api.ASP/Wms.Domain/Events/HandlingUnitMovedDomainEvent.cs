using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

/// <summary>
/// Raised once per inventory row carried along when a handling unit is relocated,
/// so the audit trail shows what stock left the source and arrived at the destination.
/// All rows of one move share the same <paramref name="MoveId"/>.
/// </summary>
public sealed record HandlingUnitMovedDomainEvent(
    Guid MoveId,
    Guid HandlingUnitId,
    Guid ProductId,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    Guid? LotId,
    int Quantity) : IDomainEvent;
