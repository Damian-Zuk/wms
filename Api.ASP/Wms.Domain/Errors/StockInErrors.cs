using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class StockInErrors
{
    public static Error ProductNotFound(Guid productId) => Error.Problem(
        "StockIn.ProductNotFound",
        $"Product with ID '{productId}' not found.");

    public static Error LocationNotFound(Guid locationId) => Error.Problem(
        "StockIn.LocationNotFound",
        $"Location with ID '{locationId}' not found.");

    public static Error LotNotFound(Guid lotId) => Error.Problem(
        "StockIn.LotNotFound",
        $"Lot with ID '{lotId}' not found.");

    public static Error NotFound(Guid stockInId) => Error.Problem(
        "StockIn.NotFound",
        $"StockIn with ID '{stockInId}' not found");

    public static Error InvalidStatusTransition(StockInStatus current, StockInStatus target) => Error.Conflict(
        "StockIn.InvalidStatusTransition",
        $"Cannot transition StockIn from '{current}' to '{target}'.");

    public static Error CannotModifyItems(StockInStatus current) => Error.Conflict(
        "StockIn.CannotModifyItems",
        $"Items can only be modified while in '{StockInStatus.Draft}' status (current: '{current}').");

    public static Error LineNotFound(Guid lineId) => Error.Problem(
        "StockIn.LineNotFound",
        $"StockIn line with ID '{lineId}' not found.");

    public static Error PlacementsRequired() => Error.Problem(
        "StockIn.PlacementsRequired",
        "At least one placement is required.");

    public static Error PlacementQuantityMustBePositive() => Error.Problem(
        "StockIn.PlacementQuantityMustBePositive",
        "Each placement quantity must be greater than 0.");

    public static Error PlacementsDoNotMatchLineTotal(Guid productId, int expected, int actual) => Error.Conflict(
        "StockIn.PlacementsDoNotMatchLineTotal",
        $"Placements for product '{productId}' total {actual} but the line requires exactly {expected}.");

    public static Error CapacityNoLongerAvailable(Guid locationId, int limit, int requestedTotal) => Error.Conflict(
        "StockIn.CapacityNoLongerAvailable",
        $"Location '{locationId}' no longer has capacity for this stock-in " +
        $"(limit: {limit}, required total: {requestedTotal}).");
}
