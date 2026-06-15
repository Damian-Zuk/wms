using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

/// <summary>
/// A single pick: a quantity drawn from one location (and the lot the planner
/// chose, if the product is lot-tracked). A line can have several of these (the
/// quantity split across locations/lots). <see cref="Strategy"/> records which
/// picking strategy produced the allocation. Product lives on the line, not here.
/// </summary>
public class StockOutItem : Entity
{
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }
    public Quantity Quantity { get; private set; } = new Quantity(0);
    public PickingStrategyType Strategy { get; private set; }

    /// <summary>The handling unit the planner pinned this pick to; null = loose stock.</summary>
    public Guid? HandlingUnitId { get; private set; }

    /// <summary>How much of <see cref="Quantity"/> has been physically picked so far.</summary>
    public Quantity PickedQuantity { get; private set; } = new Quantity(0);

    /// <summary>Units still waiting to be picked.</summary>
    public int Remaining => Quantity.Value - PickedQuantity.Value;

    public bool IsFullyPicked => PickedQuantity.Value >= Quantity.Value;

    private StockOutItem() { }

    public StockOutItem(Guid locationId, Guid? lotId, Quantity quantity, PickingStrategyType strategy, Guid? handlingUnitId = null)
    {
        Id = Guid.NewGuid();
        LocationId = locationId;
        LotId = lotId;
        Quantity = quantity;
        Strategy = strategy;
        HandlingUnitId = handlingUnitId;
    }

    /// <summary>
    /// Records that <paramref name="qty"/> units of this pick were taken. May be
    /// called repeatedly to pick in parts; the running total must never exceed the
    /// planned <see cref="Quantity"/>.
    /// </summary>
    public Result Pick(Quantity qty)
    {
        if (qty.Value <= 0)
            return StockOutErrors.AllocationQuantityMustBePositive();

        if (qty.Value > Remaining)
            return StockOutErrors.PickQuantityExceedsRemaining(Id, Remaining, qty.Value);

        PickedQuantity = new Quantity(PickedQuantity.Value + qty.Value);
        return Result.Success();
    }
}
