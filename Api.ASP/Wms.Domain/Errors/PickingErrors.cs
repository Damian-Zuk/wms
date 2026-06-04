using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Domain.Errors;

public static class PickingErrors
{
    public static Error CannotPickFullQuantity(Guid productId, PickingStrategyType strategy, int requested, int unfulfilled) => Error.Conflict(
        "Picking.CannotPickFullQuantity",
        $"Cannot pick the full quantity for product '{productId}' using {strategy}: " +
        $"requested {requested}, {unfulfilled} could not be allocated (insufficient available stock).");

    public static Error UnknownStrategy(PickingStrategyType strategy) => Error.Problem(
        "Picking.UnknownStrategy",
        $"No picking strategy is registered for type '{strategy}'.");
}
