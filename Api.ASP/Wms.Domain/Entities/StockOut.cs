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

    /// <summary>
    /// Worker has started walking the floor. Items remain reserved
    /// (reservation was made by CreateStockOut), but nothing has been
    /// physically removed yet — that happens at Pack. Pure status
    /// transition, no inventory mutation, no events.
    /// </summary>
    public Result StartPicking()
    {
        if (Status != StockOutStatus.Draft)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Picking);

        Status = StockOutStatus.Picking;
        return Result.Success();
    }

    /// <summary>
    /// Items reached the pack station. This is the point at which the
    /// physical removal is recorded — OnHand and Reserved both drop. The
    /// per-item picked event drives the StockMovement(Out) audit row.
    /// </summary>
    public Result Pack()
    {
        if (Status != StockOutStatus.Picking)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Packed);

        Status = StockOutStatus.Packed;

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
    /// Cancels the stock-out.
    /// - Draft and Picking — reservation exists, nothing physical has been
    ///   removed yet, so the handler just releases the reservation. No
    ///   return-to-stock event is raised.
    /// - Packed — physical stock was removed at Pack, so we must return it.
    ///   A per-item event drives the StockMovement(In) audit row.
    /// - Shipped or Completed — not allowed; those require a proper
    ///   returns workflow which is out of scope.
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

        if (statusBeforeCancel == StockOutStatus.Packed)
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
