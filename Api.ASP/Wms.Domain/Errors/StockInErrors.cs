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
}
