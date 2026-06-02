using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class LocationErrors
{
    public static Error CodeExists(string Code) => Error.Problem(
        "Location.CodeExists",
        $"Location with code '{Code}' already exists.");

    public static Error CodeNotFound(string Code) => Error.Problem(
        "Location.NotFound",
        $"Location with code '{Code}' not found");

    public static Error NotFound(Guid locationID) => Error.Problem(
        "Location.NotFound",
        $"Location with ID '{locationID}' not found");

    public static Error AddressExists(string address) => Error.Problem(
        "Location.AddressExists",
        $"Location with address '{address}' already exists.");

    public static Error InvalidLocationAddress(string reason) => Error.Problem(
        "Location.InvalidAddress",
        $"Invalid location address: {reason}");

    public static Error LocationBlocked(Guid locationId, string? reason) => Error.Conflict(
        "Location.Blocked",
        $"Location '{locationId}' is blocked" +
        (string.IsNullOrWhiteSpace(reason) ? "." : $": {reason}."));

    public static Error LocationInactive(Guid locationId) => Error.Conflict(
        "Location.Inactive",
        $"Location '{locationId}' is inactive.");

    public static Error TemperatureMismatch(
        Guid locationId,
        TemperatureZone locationZone,
        TemperatureZone productZone) => Error.Conflict(
        "Location.TemperatureMismatch",
        $"Location '{locationId}' temperature zone '{locationZone}' " +
        $"does not match product required zone '{productZone}'.");

    public static Error CapacityExceeded(Guid locationId, CapacityDimension dimension, int limit, int requested) => Error.Conflict(
        "Location.CapacityExceeded",
        $"Location '{locationId}' {dimension} capacity exceeded (limit: {limit}, requested total: {requested}).");

    public static Error MixedSkuNotAllowed(Guid locationId, Guid existingProductId) => Error.Conflict(
        "Location.MixedSkuNotAllowed",
        $"Location '{locationId}' does not allow mixed SKUs (currently holds product '{existingProductId}').");

    public static Error MixedLotNotAllowed(Guid locationId, Guid existingLotId) => Error.Conflict(
        "Location.MixedLotNotAllowed",
        $"Location '{locationId}' does not allow mixed lots (currently holds lot '{existingLotId}').");
}
