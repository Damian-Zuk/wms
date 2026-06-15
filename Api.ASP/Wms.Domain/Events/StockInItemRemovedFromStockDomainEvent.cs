using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

/// <summary>
/// Raised when a StockIn is cancelled after some units were already put away,
/// removing those units from a location.
/// </summary>
public sealed record StockInItemRemovedFromStockDomainEvent(
    Guid StockInId,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    int Quantity,
    Guid? HandlingUnitId = null) : IDomainEvent;
