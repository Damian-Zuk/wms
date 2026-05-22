using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class StockInItem : Entity
{
    public Guid ProductId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }
    public Quantity Quantity { get; private set; } = new Quantity(0);

    private StockInItem() { }

    public StockInItem(Guid productId, Guid locationId, Guid? lotId, Quantity quantity)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        LocationId = locationId;
        LotId = lotId;
        Quantity = quantity;
    }
}
