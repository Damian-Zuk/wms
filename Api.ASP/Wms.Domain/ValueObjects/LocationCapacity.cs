using Wms.Domain.Enums;
using Wms.Domain.Primitives;

namespace Wms.Domain.ValueObjects;

/// <summary>
/// A location's capacity expressed per <see cref="CapacityDimension"/>. Today only
/// the <see cref="CapacityDimension.Units"/> dimension exists (<see cref="MaxUnits"/>);
/// adding weight/volume later means adding a field here plus a switch arm in
/// <see cref="Limit"/> / <see cref="ConfiguredDimensions"/> and a line in the load
/// calculator — the capacity-checking logic on <see cref="Entities.Location"/> stays
/// the same. A null limit on a dimension means "unlimited" on that dimension.
/// </summary>
public class LocationCapacity : ValueObject
{
    /// <summary>Maximum number of units; null = unlimited on the Units dimension.</summary>
    public int? MaxUnits { get; }

    public LocationCapacity(int? maxUnits)
    {
        if (maxUnits is < 0)
            throw new ArgumentException("Capacity cannot be negative", nameof(maxUnits));

        MaxUnits = maxUnits;
    }

    /// <summary>An all-unlimited capacity (no dimension is constrained).</summary>
    public static LocationCapacity Unlimited { get; } = new((int?)null);

    /// <summary>The configured limit for a dimension, or null when unlimited.</summary>
    public int? Limit(CapacityDimension dimension) => dimension switch
    {
        CapacityDimension.Units => MaxUnits,
        _ => null
    };

    /// <summary>The dimensions that actually carry a finite limit.</summary>
    public IEnumerable<CapacityDimension> ConfiguredDimensions()
    {
        if (MaxUnits.HasValue)
            yield return CapacityDimension.Units;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return MaxUnits.HasValue;
        yield return MaxUnits.GetValueOrDefault();
    }
}
