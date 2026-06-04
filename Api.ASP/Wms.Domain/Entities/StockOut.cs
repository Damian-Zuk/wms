using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Events;
using Wms.Domain.Models;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class StockOut : Entity
{
    private readonly List<StockOutLine> _lines = [];
    public IReadOnlyCollection<StockOutLine> Lines => _lines;

    public StockOutStatus Status { get; private set; } = StockOutStatus.Draft;

    public StockOutStatus? CancelledFrom { get; private set; }

    private StockOut() { }

    public StockOut(Guid id)
        : base(id)
    {
    }

    /// <summary>
    /// Adds a requested line together with its planned pick allocations. The
    /// allocations must already sum to <paramref name="quantity"/> (enforced by the line).
    /// </summary>
    public Result AddLineWithAllocations(
        Guid productId,
        PickingStrategyType strategy,
        Quantity quantity,
        IReadOnlyList<PickAllocation> allocations)
    {
        if (Status != StockOutStatus.Draft)
            return StockOutErrors.CannotModifyItems(Status);

        var line = new StockOutLine(productId, strategy, quantity);
        var result = line.SetAllocations(allocations);
        if (result.IsFailure)
            return result;

        _lines.Add(line);
        return Result.Success();
    }

    /// <summary>
    /// Worker has started walking the floor. Items remain reserved (the reservation
    /// was made by CreateStockOut); picking each item then removes stock
    /// incrementally. Pure status transition, no inventory mutation, no events.
    /// </summary>
    public Result StartPicking()
    {
        if (Status != StockOutStatus.Draft)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Picking);

        Status = StockOutStatus.Picking;
        return Result.Success();
    }

    /// <summary>
    /// Records the manual pick of <paramref name="qty"/> units of a single item.
    /// Each call books those units (a picked event is raised so a stock movement is
    /// written and inventory is decremented) and may be repeated until the item is
    /// fully picked.
    /// </summary>
    public Result PickItem(Guid itemId, Quantity qty)
    {
        if (Status != StockOutStatus.Picking)
            return StockOutErrors.CannotPick(Status);

        var line = _lines.FirstOrDefault(l => l.Items.Any(i => i.Id == itemId));
        if (line is null)
            return StockOutErrors.ItemNotFound(itemId);

        var item = line.Items.First(i => i.Id == itemId);

        var result = item.Pick(qty);
        if (result.IsFailure)
            return result;

        Raise(new StockOutItemPickedDomainEvent(
            Id,
            line.ProductId,
            item.LocationId,
            item.LotId,
            qty.Value));

        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != StockOutStatus.Picking)
            return StockOutErrors.InvalidStatusTransition(Status, StockOutStatus.Completed);

        if (!_lines.All(l => l.IsFullyPicked))
            return StockOutErrors.NotAllItemsPicked();

        Status = StockOutStatus.Completed;
        return Result.Success();
    }

    /// <summary>
    /// Cancels the stock-out (allowed from Draft or Picking only) and records the
    /// phase it was cancelled from.
    /// - Draft — only reservations exist; the handler releases them.
    /// - Picking — already-picked units must be returned to stock (a per-item event
    ///   drives the StockMovement(In) audit row) and the unpicked remainder's
    ///   reservation is released by the handler.
    /// </summary>
    public Result Cancel()
    {
        var statusBeforeCancel = Status;

        if (statusBeforeCancel is not (StockOutStatus.Draft or StockOutStatus.Picking))
            return StockOutErrors.InvalidStatusTransition(statusBeforeCancel, StockOutStatus.Cancelled);

        CancelledFrom = statusBeforeCancel;
        Status = StockOutStatus.Cancelled;

        if (statusBeforeCancel == StockOutStatus.Picking)
        {
            foreach (var line in _lines)
            {
                foreach (var item in line.Items.Where(i => i.PickedQuantity.Value > 0))
                {
                    Raise(new StockOutItemReturnedToStockDomainEvent(
                        Id,
                        line.ProductId,
                        item.LocationId,
                        item.LotId,
                        item.PickedQuantity.Value));
                }
            }
        }

        return Result.Success();
    }
}
