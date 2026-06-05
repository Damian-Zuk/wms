using Wms.Domain.Enums;
using Wms.Domain.Primitives;

namespace Wms.Domain.ValueObjects;

/// <summary>
/// A location's capacity expressed per <see cref="CapacityDimension"/>: units (an
/// integer count), weight (kg) and volume (dm³). Each limit is independently nullable
/// and a null limit means "unlimited" on that dimension. The capacity-checking logic
/// on <see cref="Entities.Location"/> iterates <see cref="ConfiguredDimensions"/>, so
/// it treats all three dimensions uniformly and the most restrictive one wins.
/// </summary>
public class LocationCapacity : ValueObject
{
    /// <summary>Maximum number of units; null = unlimited on the Units dimension.</summary>
    public int? MaxUnits { get; }

    /// <summary>Maximum total weight in kilograms; null = unlimited on the Weight dimension.</summary>
    public decimal? MaxWeight { get; }

    /// <summary>Maximum total volume in cubic decimetres; null = unlimited on the Volume dimension.</summary>
    public decimal? MaxVolume { get; }

    public LocationCapacity(int? maxUnits, decimal? maxWeight = null, decimal? maxVolume = null)
    {
        if (maxUnits is < 0)
            throw new ArgumentException("Capacity cannot be negative", nameof(maxUnits));
        if (maxWeight is < 0)
            throw new ArgumentException("Capacity cannot be negative", nameof(maxWeight));
        if (maxVolume is < 0)
            throw new ArgumentException("Capacity cannot be negative", nameof(maxVolume));

        MaxUnits = maxUnits;
        MaxWeight = maxWeight;
        MaxVolume = maxVolume;
    }

    /// <summary>An all-unlimited capacity (no dimension is constrained).</summary>
    public static LocationCapacity Unlimited { get; } = new(null, null, null);

    /// <summary>The configured limit for a dimension, or null when unlimited.</summary>
    public decimal? Limit(CapacityDimension dimension) => dimension switch
    {
        CapacityDimension.Units => MaxUnits,
        CapacityDimension.Weight => MaxWeight,
        CapacityDimension.Volume => MaxVolume,
        _ => null
    };

    /// <summary>The dimensions that actually carry a finite limit.</summary>
    public IEnumerable<CapacityDimension> ConfiguredDimensions()
    {
        if (MaxUnits.HasValue)
            yield return CapacityDimension.Units;
        if (MaxWeight.HasValue)
            yield return CapacityDimension.Weight;
        if (MaxVolume.HasValue)
            yield return CapacityDimension.Volume;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return MaxUnits.HasValue;
        yield return MaxUnits.GetValueOrDefault();
        yield return MaxWeight.HasValue;
        yield return MaxWeight.GetValueOrDefault();
        yield return MaxVolume.HasValue;
        yield return MaxVolume.GetValueOrDefault();
    }
}
