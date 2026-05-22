using Wms.Domain.Enums;
using Wms.Domain.Primitives;

namespace Wms.Domain.Entities;

public class StockMovement : Entity
{
    public Guid ProductId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }

    public int QuantityChange { get; private set; }

    public StockMovementType Type { get; private set; }
    public StockMovementSource Source { get; private set; }

    public Guid SourceId { get; private set; }

    private StockMovement() { }

    public StockMovement(
        Guid productId,
        Guid locationId,
        Guid? lotId,
        int quantityChange,
        StockMovementType type,
        StockMovementSource source,
        Guid sourceId)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        LocationId = locationId;
        LotId = lotId;
        QuantityChange = quantityChange;
        Type = type;
        Source = source;
        SourceId = sourceId;
    }
}
