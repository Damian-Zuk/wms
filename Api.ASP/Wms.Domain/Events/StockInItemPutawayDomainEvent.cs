using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

public sealed record StockInItemPutawayDomainEvent(
    Guid StockInId,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
    int Quantity) : IDomainEvent;
