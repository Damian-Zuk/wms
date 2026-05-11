using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

public sealed record StockInItemAddedDomainEvent(
    Guid StockInId,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    int Quantity) : IDomainEvent;
