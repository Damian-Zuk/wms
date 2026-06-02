using Wms.Domain.Enums;

namespace Wms.Domain.Services;

/// <summary>
/// A mutable accumulator of capacity load per dimension. It represents space that
/// should count as occupied for a capacity check <em>in addition to</em> a location's
/// own on-hand inventory — for example other stock-ins' active capacity reservations,
/// or sibling placements being planned within the same draft.
/// </summary>
public sealed class CapacityOccupancy
{
    private readonly Dictionary<CapacityDimension, int> _load = [];

    public static CapacityOccupancy Empty() => new();

    public int Get(CapacityDimension dimension) => _load.GetValueOrDefault(dimension);

    public void Add(IReadOnlyDictionary<CapacityDimension, int> load)
    {
        foreach (var (dimension, value) in load)
            _load[dimension] = _load.GetValueOrDefault(dimension) + value;
    }
}
