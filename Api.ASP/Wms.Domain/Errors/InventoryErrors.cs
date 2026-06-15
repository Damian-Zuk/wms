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

    public static Error InsufficientAvailableStock(int available, int requested) => Error.Conflict(
        "Inventory.InsufficientAvailableStock",
        $"Insufficient available stock to reserve (available: {available}, requested: {requested}).");

    public static Error ReservationExceedsOnHand(int onHand, int reserved) => Error.Conflict(
        "Inventory.ReservationExceedsOnHand",
        $"Reservation would exceed on-hand stock (on-hand: {onHand}, reserved: {reserved}).");

    public static Error ReleaseExceedsReserved(int reserved, int requested) => Error.Conflict(
        "Inventory.ReleaseExceedsReserved",
        $"Cannot release more than is reserved (reserved: {reserved}, requested: {requested}).");

    public static Error AdjustmentWouldViolateReservation(int onHandAfter, int reserved) => Error.Conflict(
        "Inventory.AdjustmentWouldViolateReservation",
        $"Adjustment would push on-hand ({onHandAfter}) below reserved ({reserved}). " +
        "Release the affected reservation first.");

    public static Error ConcurrencyConflict() => Error.Conflict(
        "Inventory.ConcurrencyConflict",
        "Inventory was modified by another operation. Retry the request.");

    public static Error InsufficientAvailableStockForFefo(Guid productId, int available, int requested) => Error.Conflict(
        "Inventory.InsufficientAvailableStockForFefo",
        $"FEFO cannot fully allocate product '{productId}' " +
        $"(available across lots: {available}, requested: {requested}).");

    public static Error RebucketMustMatchProductAndLot() => Error.Problem(
        "Inventory.RebucketMustMatchProductAndLot",
        "Stock can only be re-bucketed between rows of the same product and lot.");
}
