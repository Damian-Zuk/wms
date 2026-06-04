using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class StockOutErrors
{
    public static Error ProductNotFound(Guid productId) => Error.Problem(
        "StockOut.ProductNotFound",
        $"Product with ID '{productId}' not found.");

    public static Error LocationNotFound(Guid locationId) => Error.Problem(
        "StockOut.LocationNotFound",
        $"Location with ID '{locationId}' not found.");

    public static Error LotNotFound(Guid lotId) => Error.Problem(
        "StockOut.LotNotFound",
        $"Lot with ID '{lotId}' not found.");

    public static Error InsufficientInventory(Guid productId, Guid locationId) => Error.Problem(
        "StockOut.InsufficientInventory",
        $"Insufficient inventory for product {productId} at location {locationId}.");

    public static Error NotFound(Guid stockOutId) => Error.Problem(
        "StockOut.NotFound",
        $"StockOut with ID '{stockOutId}' not found");

    public static Error InvalidStatusTransition(StockOutStatus current, StockOutStatus target) => Error.Conflict(
        "StockOut.InvalidStatusTransition",
        $"Cannot transition StockOut from '{current}' to '{target}'.");

    public static Error CannotModifyItems(StockOutStatus current) => Error.Conflict(
        "StockOut.CannotModifyItems",
        $"Items can only be modified while in '{StockOutStatus.Draft}' status (current: '{current}').");

    public static Error LineNotFound(Guid lineId) => Error.Problem(
        "StockOut.LineNotFound",
        $"StockOut line with ID '{lineId}' not found.");

    public static Error ItemNotFound(Guid itemId) => Error.Problem(
        "StockOut.ItemNotFound",
        $"StockOut item with ID '{itemId}' not found.");

    public static Error CannotPick(StockOutStatus current) => Error.Conflict(
        "StockOut.CannotPick",
        $"Items can only be picked while in '{StockOutStatus.Picking}' status (current: '{current}').");

    public static Error PickQuantityExceedsRemaining(Guid itemId, int remaining, int requested) => Error.Conflict(
        "StockOut.PickQuantityExceedsRemaining",
        $"Cannot pick {requested} units for item '{itemId}'; only {remaining} remain.");

    public static Error NotAllItemsPicked() => Error.Conflict(
        "StockOut.NotAllItemsPicked",
        "All items must be picked before the stock-out can be completed.");

    public static Error AllocationsRequired() => Error.Problem(
        "StockOut.AllocationsRequired",
        "At least one pick allocation is required.");

    public static Error AllocationQuantityMustBePositive() => Error.Problem(
        "StockOut.AllocationQuantityMustBePositive",
        "Each pick allocation quantity must be greater than 0.");

    public static Error AllocationsDoNotMatchLineTotal(Guid productId, int expected, int actual) => Error.Conflict(
        "StockOut.AllocationsDoNotMatchLineTotal",
        $"Pick allocations for product '{productId}' total {actual} but the line requires exactly {expected}.");
}
