using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

/// <summary>
/// A physical unit of handling — a pallet, box or container — identified by a unique
/// license-plate <see cref="Code"/>. Stock inside it is tracked by Inventory rows that
/// carry its id, so the HU itself stays thin: a code, a type, and where it stands.
/// A null <see cref="LocationId"/> means the unit was declared on a stock-in but has
/// not been put away yet.
/// </summary>
public class HandlingUnit : Entity
{
    public HandlingUnitCode Code { get; private set; } = null!;
    public HandlingUnitType Type { get; private set; }
    public Guid? LocationId { get; private set; }

    public bool IsPlaced => LocationId.HasValue;

    private HandlingUnit() { }

    public HandlingUnit(HandlingUnitCode code, HandlingUnitType type, Guid? locationId = null)
    {
        Id = Guid.NewGuid();
        Code = code;
        Type = type;
        LocationId = locationId;
    }

    /// <summary>
    /// Pins the unit to the location its first putaway lands in. Idempotent for repeated
    /// putaways into the same location (partial putaways of one placement); fails if the
    /// unit already stands somewhere else.
    /// </summary>
    public Result PlaceAt(Guid locationId)
    {
        if (LocationId == locationId)
            return Result.Success();

        if (LocationId.HasValue)
            return HandlingUnitErrors.AlreadyPlacedElsewhere(Id, LocationId.Value);

        LocationId = locationId;
        return Result.Success();
    }

    /// <summary>
    /// Relocates a placed unit. The caller moves the unit's inventory rows alongside —
    /// this only re-points the unit itself.
    /// </summary>
    public Result MoveTo(Guid locationId)
    {
        if (!LocationId.HasValue)
            return HandlingUnitErrors.NotPlaced(Id);

        if (LocationId == locationId)
            return HandlingUnitErrors.SameLocation();

        LocationId = locationId;
        return Result.Success();
    }
}
