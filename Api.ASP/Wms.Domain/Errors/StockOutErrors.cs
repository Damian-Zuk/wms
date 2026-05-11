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
}
