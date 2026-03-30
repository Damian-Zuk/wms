using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class Inventory : Entity
{
    public Guid ProductId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }

    public Quantity Quantity { get; private set; } = new Quantity(0);

    private Inventory() { }

    public Inventory(Guid productId, Guid locationId, Guid? lotId = null)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        LocationId = locationId;
        LotId = lotId;
        Quantity = new Quantity(0);
    }

    public void Increase(Quantity qty)
    {
        Quantity = Quantity.Add(qty);
    }

    public void Decrease(Quantity qty)
    {
        Quantity = Quantity.Subtract(qty);
    }
}
