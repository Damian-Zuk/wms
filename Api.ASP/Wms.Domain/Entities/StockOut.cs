using Wms.Domain.Events;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class StockOut : Entity
{
    private readonly List<StockOutItem> _items = [];
    public IReadOnlyCollection<StockOutItem> Items => _items;

    private StockOut() { }

    public StockOut(Guid id) 
        : base(id)
    {
    }

    public void AddItem(Guid productId, Guid locationId, Guid? lotId, Quantity quantity)
    {
        _items.Add(new StockOutItem(productId, locationId, lotId, quantity));
        Raise(new StockOutItemAddedDomainEvent(Id, productId, locationId, lotId, quantity.Value));
    }
}
