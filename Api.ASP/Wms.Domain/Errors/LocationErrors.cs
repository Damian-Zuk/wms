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
}
