using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class StockTransferErrors
{
    public static Error ProductNotFound(Guid productId) => Error.Problem(
        "StockTransfer.ProductNotFound",
        $"Product with ID '{productId}' not found.");

    public static Error LocationNotFound(Guid locationId) => Error.Problem(
        "StockTransfer.LocationNotFound",
        $"Location with ID '{locationId}' not found.");

    public static Error LotNotFound(Guid lotId) => Error.Problem(
        "StockTransfer.LotNotFound",
        $"Lot with ID '{lotId}' not found.");

    public static Error SameSourceAndDestination() => Error.Problem(
        "StockTransfer.SameSourceAndDestination",
        "Source and destination locations must be different.");

    public static Error SourceInventoryNotFound(Guid productId, Guid locationId, Guid? lotId) => Error.Problem(
        "StockTransfer.SourceInventoryNotFound",
        $"No inventory exists for product '{productId}' at location '{locationId}'" +
        (lotId.HasValue ? $" with lot '{lotId.Value}'." : "."));
}
