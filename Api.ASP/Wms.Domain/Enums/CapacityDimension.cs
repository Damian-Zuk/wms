namespace Wms.Domain.Enums;

/// <summary>
/// A physical dimension along which a location's capacity can be constrained.
/// Only <see cref="Units"/> is enforced today; Weight/Volume can be added later
/// by extending this enum, <see cref="ValueObjects.LocationCapacity"/> and the
/// load calculator without touching the capacity-checking logic.
/// </summary>
public enum CapacityDimension
{
    Units = 1
}
