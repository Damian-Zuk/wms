using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

/// <summary>
/// An inbound reservation of physical capacity at a location for one stock-in
/// placement. Created when a stock-in starts receiving and counted against the
/// location's available capacity for as long as the row exists. The row is
/// hard-deleted once its units become on-hand inventory (receive) or the hold is
/// abandoned (cancel) — it is transient coordination state, and the permanent
/// record of a receipt is the <see cref="StockMovement"/>. This is distinct from
/// <see cref="Inventory.Reserved"/>, which reserves existing stock for outbound work.
/// </summary>
public class CapacityReservation : Entity
{
    public Guid StockInId { get; private set; }
    public Guid StockInItemId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? LotId { get; private set; }
    public Quantity Quantity { get; private set; } = new Quantity(0);

    private CapacityReservation() { }

    public CapacityReservation(
        Guid stockInId,
        Guid stockInItemId,
        Guid locationId,
        Guid productId,
        Guid? lotId,
        Quantity quantity)
    {
        Id = Guid.NewGuid();
        StockInId = stockInId;
        StockInItemId = stockInItemId;
        LocationId = locationId;
        ProductId = productId;
        LotId = lotId;
        Quantity = quantity;
    }

    /// <summary>
    /// Releases <paramref name="qty"/> units of the held capacity as those units
    /// become on-hand inventory. The caller deletes the row once it reaches zero.
    /// </summary>
    public Result Reduce(Quantity qty)
    {
        if (qty.Value <= 0)
            return StockInErrors.PlacementQuantityMustBePositive();

        if (qty.Value > Quantity.Value)
            return StockInErrors.PutawayQuantityExceedsRemaining(StockInItemId, Quantity.Value, qty.Value);

        Quantity = new Quantity(Quantity.Value - qty.Value);
        return Result.Success();
    }
}
