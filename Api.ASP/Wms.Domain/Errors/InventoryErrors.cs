using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class InventoryErrors
{
    public static Error NotFound(Guid inventoryId) => Error.Problem(
        "Inventory.NotFound",
        $"Inventory with ID '{inventoryId}' not found");

    public static Error AdjustmentMustBeNonZero() => Error.Problem(
        "Inventory.AdjustmentMustBeNonZero",
        "Adjustment quantity change must not be zero.");

    public static Error InsufficientQuantity(int available, int requested) => Error.Conflict(
        "Inventory.InsufficientQuantity",
        $"Insufficient quantity available (available: {available}, requested: {requested}).");
}
