using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class HandlingUnitErrors
{
    public static Error NotFound(Guid handlingUnitId) => Error.Problem(
        "HandlingUnit.NotFound",
        $"Handling unit with ID '{handlingUnitId}' not found.");

    public static Error LocationNotFound(Guid locationId) => Error.Problem(
        "HandlingUnit.LocationNotFound",
        $"Location with ID '{locationId}' not found.");

    public static Error ProductNotFound(Guid productId) => Error.Problem(
        "HandlingUnit.ProductNotFound",
        $"Product with ID '{productId}' not found.");

    public static Error CodeAlreadyExists(string code) => Error.Problem(
        "HandlingUnit.CodeAlreadyExists",
        $"A handling unit with code '{code}' already exists.");

    public static Error NotPlaced(Guid handlingUnitId) => Error.Problem(
        "HandlingUnit.NotPlaced",
        $"Handling unit '{handlingUnitId}' has not been put away to a location yet.");

    public static Error AlreadyPlacedElsewhere(Guid handlingUnitId, Guid currentLocationId) => Error.Problem(
        "HandlingUnit.AlreadyPlacedElsewhere",
        $"Handling unit '{handlingUnitId}' already stands at location '{currentLocationId}'.");

    public static Error SameLocation() => Error.Problem(
        "HandlingUnit.SameLocation",
        "Source and destination locations must be different.");

    public static Error HasReservedStock(Guid handlingUnitId) => Error.Problem(
        "HandlingUnit.HasReservedStock",
        $"Handling unit '{handlingUnitId}' holds stock reserved for outbound work and cannot be moved.");

    public static Error NotEmpty(Guid handlingUnitId) => Error.Problem(
        "HandlingUnit.NotEmpty",
        $"Handling unit '{handlingUnitId}' still holds stock and cannot be deleted.");

    public static Error InUseByActiveDocuments(Guid handlingUnitId) => Error.Problem(
        "HandlingUnit.InUseByActiveDocuments",
        $"Handling unit '{handlingUnitId}' is referenced by an active stock-in or stock-out and cannot be deleted.");

    public static Error NotAtLocation(Guid handlingUnitId, Guid locationId) => Error.Problem(
        "HandlingUnit.NotAtLocation",
        $"Handling unit '{handlingUnitId}' does not stand at location '{locationId}'.");

    public static Error InsufficientLooseStock(Guid productId, Guid locationId, int available, int requested) => Error.Problem(
        "HandlingUnit.InsufficientLooseStock",
        $"Only {available} loose unit(s) of product '{productId}' are available at location '{locationId}', requested {requested}.");

    public static Error DeclaredQuantitiesExceedLine(Guid productId, int lineQuantity, int declaredTotal) => Error.Problem(
        "HandlingUnit.DeclaredQuantitiesExceedLine",
        $"Declared handling unit quantities ({declaredTotal}) exceed the line quantity ({lineQuantity}) for product '{productId}'.");

    public static Error HandlingUnitSplitAcrossPlacements(Guid handlingUnitId) => Error.Problem(
        "HandlingUnit.SplitAcrossPlacements",
        $"Handling unit '{handlingUnitId}' must be placed whole into a single location.");

    public static Error PlacementsMustPreserveHandlingUnits(Guid lineId) => Error.Problem(
        "HandlingUnit.PlacementsMustPreserveHandlingUnits",
        $"Placements for line '{lineId}' must keep one placement per declared handling unit with its declared quantity (only the location may change).");
}
