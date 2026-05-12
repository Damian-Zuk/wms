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
}
