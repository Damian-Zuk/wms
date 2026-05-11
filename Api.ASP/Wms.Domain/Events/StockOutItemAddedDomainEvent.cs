using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

public sealed record StockOutItemAddedDomainEvent(
    Guid StockOutId,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    int Quantity) : IDomainEvent;
