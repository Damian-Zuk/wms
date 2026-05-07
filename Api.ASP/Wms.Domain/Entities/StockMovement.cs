using Wms.Domain.Enums;
using Wms.Domain.Primitives;

namespace Wms.Domain.Entities;

public class StockMovement : Entity
{
    public Guid ProductId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? LotId { get; set; }

    public int QuantityChange { get; set; }

    public StockMovementType Type { get; set; }
    public StockMovementSource Source { get; set; }

    public Guid SourceId { get; set; }

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
