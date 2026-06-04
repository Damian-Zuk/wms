using Wms.Domain.Errors;
using Wms.Domain.Events;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class Inventory : Entity
{
    public Guid ProductId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? LotId { get; private set; }

    /// <summary>Physical units present in the location.</summary>
    public Quantity OnHand { get; private set; } = new Quantity(0);

    /// <summary>Units committed to outbound work (still physically present).</summary>
    public Quantity Reserved { get; private set; } = new Quantity(0);

    /// <summary>Units that can still be promised.</summary>
    public Quantity Available => new(OnHand.Value - Reserved.Value);

    public DateTime? ReceivedAt { get; private set; }

    private Inventory() { }

    public Inventory(Guid productId, Guid locationId, Guid? lotId = null)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        LocationId = locationId;
        LotId = lotId;
        OnHand = new Quantity(0);
        Reserved = new Quantity(0);
    }

    /// <summary>
    /// Adds physical stock. Used by cancel-after-pick return-to-stock. Does not
    /// touch Reserved or <see cref="ReceivedAt"/>.
    /// </summary>
    public void Increase(Quantity qty)
    {
        OnHand = OnHand.Add(qty);
    }

    /// <summary>
    /// Receives physical stock from a putaway. Adds to OnHand and stamps
    /// <see cref="ReceivedAt"/> on the first receipt into this location+lot bucket
    /// (later top-ups keep the original date so FIFO ages by the oldest units).
    /// Does not touch Reserved.
    /// </summary>
    public void Receive(Quantity qty, DateTime receivedAt)
    {
        OnHand = OnHand.Add(qty);
        ReceivedAt ??= receivedAt;
    }

    /// <summary>
    /// Commits stock to outbound work. Fails when there isn't enough
    /// Available to cover the request.
    /// </summary>
    public Result Reserve(Quantity qty)
    {
        if (qty.Value > Available.Value)
            return InventoryErrors.InsufficientAvailableStock(Available.Value, qty.Value);

        Reserved = Reserved.Add(qty);
        return Result.Success();
    }

    /// <summary>
    /// Releases a previously made reservation (e.g. when a draft stock-out
    /// is cancelled). Fails when the release amount exceeds Reserved.
    /// </summary>
    public Result ReleaseReservation(Quantity qty)
    {
        if (qty.Value > Reserved.Value)
            return InventoryErrors.ReleaseExceedsReserved(Reserved.Value, qty.Value);

        Reserved = Reserved.Subtract(qty);
        return Result.Success();
    }

    /// <summary>
    /// Converts a reservation into a physical removal at picking time:
    /// decrements both OnHand and Reserved.
    /// </summary>
    public Result Pick(Quantity qty)
    {
        if (qty.Value > Reserved.Value)
            return InventoryErrors.ReleaseExceedsReserved(Reserved.Value, qty.Value);

        if (qty.Value > OnHand.Value)
            return InventoryErrors.InsufficientQuantity(OnHand.Value, qty.Value);

        OnHand = OnHand.Subtract(qty);
        Reserved = Reserved.Subtract(qty);
        return Result.Success();
    }

    /// <summary>
    /// Manual on-hand correction (cycle count, write-off, found stock, etc.).
    /// Positive change adds to OnHand; negative change subtracts. A negative
    /// change that would push OnHand below Reserved is rejected with
    /// AdjustmentWouldViolateReservation; release the reservation first.
    /// </summary>
    public Result Adjust(int quantityChange, string? reason)
    {
        if (quantityChange == 0)
            return InventoryErrors.AdjustmentMustBeNonZero();

        if (quantityChange > 0)
        {
            OnHand = OnHand.Add(new Quantity(quantityChange));
        }
        else
        {
            var absoluteChange = Math.Abs(quantityChange);

            if (OnHand.Value < absoluteChange)
                return InventoryErrors.InsufficientQuantity(OnHand.Value, absoluteChange);

            var onHandAfter = OnHand.Value - absoluteChange;
            if (onHandAfter < Reserved.Value)
                return InventoryErrors.AdjustmentWouldViolateReservation(onHandAfter, Reserved.Value);

            OnHand = OnHand.Subtract(new Quantity(absoluteChange));
        }

        Raise(new InventoryAdjustedDomainEvent(
            Id,
            ProductId,
            LocationId,
            LotId,
            quantityChange,
            reason));

        return Result.Success();
    }

    /// <summary>
    /// Moves physical stock between locations. Only Available stock at the
    /// source may transfer — reserved units stay where they are because the
    /// reservation points at the source inventory row. The pick flow that
    /// owns that reservation would break if we let the physical stock
    /// disappear from under it. Reserved is unchanged on both sides.
    /// </summary>
    public Result TransferTo(Inventory destination, Quantity quantity, Guid transferId)
    {
        if (LocationId == destination.LocationId)
            return StockTransferErrors.SameSourceAndDestination();

        if (quantity.Value > Available.Value)
            return InventoryErrors.InsufficientAvailableStock(Available.Value, quantity.Value);

        OnHand = OnHand.Subtract(quantity);
        destination.OnHand = destination.OnHand.Add(quantity);

        Raise(new StockTransferredDomainEvent(
            transferId,
            ProductId,
            LocationId,
            destination.LocationId,
            LotId,
            quantity.Value));

        return Result.Success();
    }
}
