using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class LotErrors
{
    public static Error NumberExists(string Number) => Error.Problem(
        "Lot.NumberExists",
        $"Lot with number '{Number}' already exists.");

    public static Error EmptyNumber => Error.Problem(
        "Lot.EmptyNumber",
        $"Lot number cannot be empty.");

    public static Error InvalidDates => Error.Problem(
        "Lot.InvalidDates",
        "Expiry date cannot be before manufactured date");

    public static Error ProductNotFound(Guid lotId) => Error.Problem(
        "Lot.ProductNotFound",
        $"Product for lot with ID '{lotId}' not found");

    public static Error NotFound(Guid lotId) => Error.Problem(
        "Lot.NotFound",
        $"Lot with ID '{lotId}' not found");
}
