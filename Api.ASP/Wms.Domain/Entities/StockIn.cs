using Wms.Domain.Events;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class StockIn : Entity
{
    private readonly List<StockInItem> _items = [];
    public IReadOnlyCollection<StockInItem> Items => _items;

    private StockIn() { }

    public StockIn(Guid id)
        : base(id)
    {
    }

    public void AddItem(Guid productId, Guid locationId, Guid? lotId, Quantity quantity)
    {
        _items.Add(new StockInItem(productId, locationId, lotId, quantity));
        Raise(new StockInItemAddedDomainEvent(Id, productId, locationId, lotId, quantity.Value));
    }
}
