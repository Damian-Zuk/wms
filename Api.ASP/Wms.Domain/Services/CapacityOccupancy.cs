using Wms.Domain.Enums;

namespace Wms.Domain.Services;

/// <summary>
/// A mutable accumulator of capacity load per dimension, together with the SKUs and
/// lots that load belongs to. It represents space that should count as occupied for a
/// capacity check <em>in addition to</em> a location's own on-hand inventory — for
/// example other stock-ins' active capacity reservations, or sibling placements being
/// planned within the same draft. The tracked product/lot identities let a mixed-SKU /
/// mixed-lot check see pending items that are not yet on-hand inventory.
/// </summary>
public sealed class CapacityOccupancy
{
    private readonly Dictionary<CapacityDimension, decimal> _load = [];
    private readonly HashSet<Guid> _productIds = [];
    private readonly HashSet<Guid> _lotIds = [];

    public static CapacityOccupancy Empty() => new();

    public decimal Get(CapacityDimension dimension) => _load.GetValueOrDefault(dimension);

    public void Add(IReadOnlyDictionary<CapacityDimension, decimal> load, Guid productId, Guid? lotId)
    {
        foreach (var (dimension, value) in load)
            _load[dimension] = _load.GetValueOrDefault(dimension) + value;

        _productIds.Add(productId);
        if (lotId.HasValue)
            _lotIds.Add(lotId.Value);
    }

    /// <summary>The first occupying product that is not <paramref name="productId"/>, or null.</summary>
    public Guid? OtherProduct(Guid productId)
    {
        foreach (var id in _productIds)
            if (id != productId)
                return id;

        return null;
    }

    /// <summary>
    /// The first occupying lot that is not <paramref name="lotId"/>, or null. Only real
    /// lot ids are tracked, so a lotless incoming item (null) conflicts with any of them.
    /// </summary>
    public Guid? OtherLot(Guid? lotId)
    {
        foreach (var id in _lotIds)
            if (id != lotId)
                return id;

        return null;
    }
}
