using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class InventoryErrors
{
    public static Error NotFound(Guid inventoryId) => Error.Problem(
        "Inventory.NotFound",
        $"Inventory with ID '{inventoryId}' not found");
}
