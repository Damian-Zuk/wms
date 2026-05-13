using Wms.Domain.Primitives;

namespace Wms.Domain.Events;

public sealed record StockTransferredDomainEvent(
    Guid TransferId,
    Guid ProductId,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    Guid? LotId,
    int Quantity) : IDomainEvent;
