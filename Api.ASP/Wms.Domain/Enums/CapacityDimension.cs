namespace Wms.Domain.Enums;

/// <summary>
/// A physical dimension along which a location's capacity can be constrained.
/// Each dimension is enforced independently: a location may cap any subset of them,
/// and the most restrictive cap wins. <see cref="ValueObjects.LocationCapacity"/>
/// holds the per-dimension limits and the load calculator turns a product quantity
/// into a load per dimension.
/// </summary>
public enum CapacityDimension
{
    /// <summary>Whole physical units (an integer count).</summary>
    Units = 1,

    /// <summary>Weight in kilograms.</summary>
    Weight = 2,

    /// <summary>Volume in cubic decimetres (litres).</summary>
    Volume = 3
}
