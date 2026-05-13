using Wms.Domain.Errors;
using Wms.Domain.Events;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class Inventory : Entity
{
    public Guid ProductId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? LotId { get; set; }

    public Quantity Quantity { get; set; } = new Quantity(0);

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

    public Result Adjust(int quantityChange, string? reason)
    {
        if (quantityChange == 0)
            return InventoryErrors.AdjustmentMustBeNonZero();

        if (quantityChange > 0)
        {
            Increase(new Quantity(quantityChange));
        }
        else
        {
            var absoluteChange = Math.Abs(quantityChange);
            if (Quantity.Value < absoluteChange)
                return InventoryErrors.InsufficientQuantity(Quantity.Value, absoluteChange);

            Decrease(new Quantity(absoluteChange));
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

    public Result TransferTo(Inventory destination, Quantity quantity, Guid transferId)
    {
        if (LocationId == destination.LocationId)
            return StockTransferErrors.SameSourceAndDestination();

        if (Quantity.Value < quantity.Value)
            return InventoryErrors.InsufficientQuantity(Quantity.Value, quantity.Value);

        Decrease(quantity);
        destination.Increase(quantity);

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
