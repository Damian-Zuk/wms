using Wms.Domain.Enums;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

/// <summary>
/// A single placement: a quantity of its parent <see cref="StockInLine"/>'s product
/// to be put away into one location. A line can have several of these (the quantity
/// split across locations). <see cref="Strategy"/> records which putaway strategy
/// produced the placement, or <see cref="PutawayStrategyType.Manual"/> if a user
/// overrode it. Product and lot live on the line, not here.
/// </summary>
public class StockInItem : Entity
{
    public Guid LocationId { get; private set; }
    public Quantity Quantity { get; private set; } = new Quantity(0);
    public PutawayStrategyType Strategy { get; private set; }

    private StockInItem() { }

    public StockInItem(Guid locationId, Quantity quantity, PutawayStrategyType strategy)
    {
        Id = Guid.NewGuid();
        LocationId = locationId;
        Quantity = quantity;
        Strategy = strategy;
    }
}
