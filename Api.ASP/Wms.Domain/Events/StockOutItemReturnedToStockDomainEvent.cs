using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

/// <summary>
/// Raised when a StockOut is cancelled after physical pick
/// has occurred, returning stock to a location.
/// </summary>
public sealed record StockOutItemReturnedToStockDomainEvent(
    Guid StockOutId,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    int Quantity) : IDomainEvent;
