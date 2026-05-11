using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class StockMovementErrors
{
    public static Error NotFound(Guid stockMovementId) => Error.Problem(
        "StockMovement.NotFound",
        $"StockMovement with ID '{stockMovementId}' not found");
}
