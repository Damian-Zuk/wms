using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Events;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Domain.Models;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class StockIn : Entity
{
    private readonly List<StockInLine> _lines = [];
    public IReadOnlyCollection<StockInLine> Lines => _lines;

    public StockInStatus Status { get; private set; } = StockInStatus.Draft;

    /// <summary>The phase the stock-in was in when cancelled (null unless cancelled).</summary>
    public StockInStatus? CancelledFrom { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Who last modified the putaway placements (null until a user edits them).</summary>
    public string? ModifiedBy { get; private set; }
    public DateTime? ModifiedAt { get; private set; }

    private StockIn() { }

    public StockIn(Guid id)
        : base(id)
    {
    }

    public void SetDescription(string? description) => Description = description;

    /// <summary>
    /// Adds a requested line together with its planned placements. The placements
    /// must already sum to <paramref name="quantity"/> (enforced by the line).
    /// </summary>
    public Result AddLineWithPlacements(
        Guid productId,
        Guid? lotId,
        Quantity quantity,
        IReadOnlyList<PlacementAllocation> placements)
    {
        if (Status != StockInStatus.Draft)
            return StockInErrors.CannotModifyItems(Status);

        var line = new StockInLine(productId, lotId, quantity);
        var result = line.SetPlacements(placements);
        if (result.IsFailure)
            return result;

        _lines.Add(line);
        return Result.Success();
    }

    /// <summary>
    /// Replaces a line's placements with a user-supplied set and records who did it.
    /// The new placements must still sum to the line's requested quantity.
    /// </summary>
    public Result ModifyLinePlacements(
        Guid lineId,
        IEnumerable<(Guid LocationId, int Quantity)> placements,
        string? modifiedBy,
        DateTime modifiedAt)
    {
        if (Status != StockInStatus.Draft)
            return StockInErrors.CannotModifyItems(Status);

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return StockInErrors.LineNotFound(lineId);

        var result = line.ReplacePlacementsManual(placements);
        if (result.IsFailure)
            return result;

        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;
        return Result.Success();
    }

    /// <summary>
    /// Replaces a line's placements with a freshly computed putaway plan, each carrying
    /// the strategy that produced it (Draft only). Unlike <see cref="ModifyLinePlacements"/>
    /// this is the system re-running the planner, not a manual override, so it does not
    /// stamp the placements Manual nor record a modifier.
    /// </summary>
    public Result ReplanLinePlacements(Guid lineId, IReadOnlyList<PlacementAllocation> placements)
    {
        if (Status != StockInStatus.Draft)
            return StockInErrors.CannotModifyItems(Status);

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return StockInErrors.LineNotFound(lineId);

        return line.SetPlacements(placements);
    }

    public Result StartPutaway()
    {
        if (Status != StockInStatus.Draft)
            return StockInErrors.InvalidStatusTransition(Status, StockInStatus.Putaway);

        Status = StockInStatus.Putaway;
        return Result.Success();
    }

    /// <summary>
    /// Records the manual putaway of <paramref name="qty"/> units of a single placement.
    /// Each call books those units (a received event is raised so a stock movement is
    /// written) and may be repeated until the placement is fully placed.
    /// </summary>
    public Result PutawayItem(Guid itemId, Quantity qty)
    {
        if (Status != StockInStatus.Putaway)
            return StockInErrors.CannotPutaway(Status);

        var line = _lines.FirstOrDefault(l => l.Items.Any(i => i.Id == itemId));
        if (line is null)
            return StockInErrors.ItemNotFound(itemId);

        var item = line.Items.First(i => i.Id == itemId);

        var result = item.Putaway(qty);
        if (result.IsFailure)
            return result;

        Raise(new StockInItemPutawayDomainEvent(
            Id,
            line.ProductId,
            item.LocationId,
            line.LotId,
            qty.Value));

        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != StockInStatus.Putaway)
            return StockInErrors.InvalidStatusTransition(Status, StockInStatus.Completed);

        if (!_lines.All(l => l.IsFullyPlaced))
            return StockInErrors.NotAllItemsPlaced();

        Status = StockInStatus.Completed;
        return Result.Success();
    }

    /// <summary>
    /// Cancels the stock-in (allowed from Draft or Putaway only) and records the
    /// phase it was cancelled from.
    /// - Draft — only capacity holds exist; the handler releases them.
    /// - Putaway — already-placed units must be removed from stock (a per-item event
    ///   drives the StockMovement(Out) audit row) and the not-yet-placed holds are
    ///   released by the handler.
    /// </summary>
    public Result Cancel()
    {
        var statusBeforeCancel = Status;

        if (statusBeforeCancel is not (StockInStatus.Draft or StockInStatus.Putaway))
            return StockInErrors.InvalidStatusTransition(statusBeforeCancel, StockInStatus.Cancelled);

        CancelledFrom = statusBeforeCancel;
        Status = StockInStatus.Cancelled;

        if (statusBeforeCancel == StockInStatus.Putaway)
        {
            foreach (var line in _lines)
            {
                foreach (var item in line.Items.Where(i => i.PlacedQuantity.Value > 0))
                {
                    Raise(new StockInItemRemovedFromStockDomainEvent(
                        Id,
                        line.ProductId,
                        item.LocationId,
                        line.LotId,
                        item.PlacedQuantity.Value));
                }
            }
        }

        return Result.Success();
    }
}
