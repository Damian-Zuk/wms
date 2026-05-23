using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Events;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class StockOut : Entity
{
    private readonly List<StockOutItem> _items = [];
    public IReadOnlyCollection<StockOutItem> Items => _items;

    public StockOutStatus Status { get; private set; } = StockOutStatus.Draft;

    private StockOut() { }

    public StockOut(Guid id)
        : base(id)
    {
    }

    public Result AddItem(Guid productId, Guid locationId, Guid? lotId, Quantity quantity)
    {
        if (Status != StockOutStatus.Draft)
            return StockOutErrors.CannotModifyItems(Status);

        _items.Add(new StockOutItem(productId, locationId, lotId, quantity));
        return Result.Success();
    }

    public Result StartPicking()
    {
        if (Status != StockOutStatus.Draft)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Picking);

        Status = StockOutStatus.Picking;

        foreach (var item in _items)
        {
            Raise(new StockOutItemPickedDomainEvent(
                Id,
                item.ProductId,
                item.LocationId,
                item.LotId,
                item.Quantity.Value));
        }

        return Result.Success();
    }

    public Result Pack()
    {
        if (Status != StockOutStatus.Picking)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Packed);

        Status = StockOutStatus.Packed;
        return Result.Success();
    }

    public Result Ship()
    {
        if (Status != StockOutStatus.Packed)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Shipped);

        Status = StockOutStatus.Shipped;
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != StockOutStatus.Shipped)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Completed);

        Status = StockOutStatus.Completed;
        return Result.Success();
    }

    /// <summary>
    /// Cancels the stock-out. Allowed from Draft (stock was reserved, not
    /// physically removed) or from Picking/Packed (stock was physically
    /// removed — the cancel must return it). Not allowed from Shipped or
    /// Completed: those require a proper returns workflow which is out of scope.
    /// </summary>
    public Result Cancel()
    {
        var statusBeforeCancel = Status;

        if (statusBeforeCancel is not (
            StockOutStatus.Draft or StockOutStatus.Picking or StockOutStatus.Packed))
        {
            return StockOutErrors.InvalidStatusTransition(statusBeforeCancel, StockOutStatus.Cancelled);
        }

        Status = StockOutStatus.Cancelled;

        if (statusBeforeCancel is StockOutStatus.Picking or StockOutStatus.Packed)
        {
            foreach (var item in _items)
            {
                Raise(new StockOutItemReturnedToStockDomainEvent(
                    Id,
                    item.ProductId,
                    item.LocationId,
                    item.LotId,
                    item.Quantity.Value));
            }
        }

        return Result.Success();
    }
}
