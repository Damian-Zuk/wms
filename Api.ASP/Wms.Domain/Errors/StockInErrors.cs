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
}
