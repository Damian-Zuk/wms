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

    /// <summary>Who last modified the putaway placements (null until a user edits them).</summary>
    public string? ModifiedBy { get; private set; }
    public DateTime? ModifiedAt { get; private set; }

    private StockIn() { }

    public StockIn(Guid id)
        : base(id)
    {
    }

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

    public Result StartReceiving()
    {
        if (Status != StockInStatus.Draft)
            return StockInErrors.InvalidStatusTransition(Status, StockInStatus.Receiving);

        Status = StockInStatus.Receiving;
        return Result.Success();
    }

    public Result Receive()
    {
        if (Status != StockInStatus.Receiving)
            return StockInErrors.InvalidStatusTransition(Status, StockInStatus.Received);

        Status = StockInStatus.Received;

        foreach (var line in _lines)
        {
            foreach (var item in line.Items)
            {
                Raise(new StockInItemReceivedDomainEvent(
                    Id,
                    line.ProductId,
                    item.LocationId,
                    line.LotId,
                    item.Quantity.Value));
            }
        }

        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != StockInStatus.Received)
            return StockInErrors.InvalidStatusTransition(Status, StockInStatus.Completed);

        Status = StockInStatus.Completed;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status is not (StockInStatus.Draft or StockInStatus.Receiving))
            return StockInErrors.InvalidStatusTransition(Status, StockInStatus.Cancelled);

        Status = StockInStatus.Cancelled;
        return Result.Success();
    }
}
