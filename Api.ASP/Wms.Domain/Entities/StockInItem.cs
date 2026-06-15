using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

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

    /// <summary>The declared handling unit this placement receives onto; null = loose stock.</summary>
    public Guid? HandlingUnitId { get; private set; }

    /// <summary>How much of <see cref="Quantity"/> has been physically put away so far.</summary>
    public Quantity PlacedQuantity { get; private set; } = new Quantity(0);

    /// <summary>Units still waiting to be put away.</summary>
    public int Remaining => Quantity.Value - PlacedQuantity.Value;

    public bool IsFullyPlaced => PlacedQuantity.Value >= Quantity.Value;

    private StockInItem() { }

    public StockInItem(Guid locationId, Quantity quantity, PutawayStrategyType strategy, Guid? handlingUnitId = null)
    {
        Id = Guid.NewGuid();
        LocationId = locationId;
        Quantity = quantity;
        Strategy = strategy;
        HandlingUnitId = handlingUnitId;
    }

    /// <summary>
    /// Records that <paramref name="qty"/> units of this placement were put away.
    /// May be called repeatedly to place the placement in parts; the running total
    /// must never exceed the planned <see cref="Quantity"/>.
    /// </summary>
    public Result Putaway(Quantity qty)
    {
        if (qty.Value <= 0)
            return StockInErrors.PlacementQuantityMustBePositive();

        if (qty.Value > Remaining)
            return StockInErrors.PutawayQuantityExceedsRemaining(Id, Remaining, qty.Value);

        PlacedQuantity = new Quantity(PlacedQuantity.Value + qty.Value);
        return Result.Success();
    }
}
