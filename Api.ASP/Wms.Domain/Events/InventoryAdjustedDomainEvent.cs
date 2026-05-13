using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

public sealed record InventoryAdjustedDomainEvent(
    Guid InventoryId,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    int QuantityChange,
    string? Reason) : IDomainEvent;
