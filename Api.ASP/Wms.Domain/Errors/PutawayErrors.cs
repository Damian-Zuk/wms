using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class PutawayErrors
{
    public static Error ProductNotFound(Guid productId) => Error.Problem(
        "Putaway.ProductNotFound",
        $"Product with ID '{productId}' not found.");

    public static Error LotNotFound(Guid lotId) => Error.Problem(
        "Putaway.LotNotFound",
        $"Lot with ID '{lotId}' not found.");

    public static Error NoSuitablePutawayLocation(Guid productId, Guid? lotId, int quantity) => Error.Conflict(
        "Putaway.NoSuitableLocation",
        $"No suitable putaway location found for product '{productId}'" +
        (lotId.HasValue ? $" (lot '{lotId.Value}')" : string.Empty) +
        $" and quantity {quantity}.");

    public static Error CannotPlaceFullQuantity(Guid productId, Guid? lotId, int requested, int unplaced) => Error.Conflict(
        "Putaway.CannotPlaceFullQuantity",
        $"Cannot place the full quantity for product '{productId}'" +
        (lotId.HasValue ? $" (lot '{lotId.Value}')" : string.Empty) +
        $": requested {requested}, {unplaced} could not be placed (insufficient capacity).");

    public static Error NoSingleLocationFitsHandlingUnit(Guid productId, Guid? lotId, int quantity) => Error.Conflict(
        "Putaway.NoSingleLocationFitsHandlingUnit",
        $"No single location can hold a handling unit of {quantity} unit(s) of product '{productId}'" +
        (lotId.HasValue ? $" (lot '{lotId.Value}')." : "."));
}
