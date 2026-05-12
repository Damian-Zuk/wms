using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Events;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class StockIn : Entity
{
    private readonly List<StockInItem> _items = [];
    public IReadOnlyCollection<StockInItem> Items => _items;

    public StockInStatus Status { get; private set; } = StockInStatus.Draft;

    private StockIn() { }

    public StockIn(Guid id)
        : base(id)
    {
    }

    public Result AddItem(Guid productId, Guid locationId, Guid? lotId, Quantity quantity)
    {
        if (Status != StockInStatus.Draft)
            return StockInErrors.CannotModifyItems(Status);

        _items.Add(new StockInItem(productId, locationId, lotId, quantity));
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

        foreach (var item in _items)
        {
            Raise(new StockInItemReceivedDomainEvent(
                Id,
                item.ProductId,
                item.LocationId,
                item.LotId,
                item.Quantity.Value));
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
