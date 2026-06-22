using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Services;
using Wms.Domain.ValueObjects;

namespace Wms.Application.Putaway;

/// <summary>
/// A consistent, in-memory snapshot the putaway planner works against: every
/// location, its current on-hand inventory, and the capacity already occupied by
/// other stock-ins' active reservations. As the planner places quantity it
/// <see cref="Commit"/>s the load back into the snapshot so that subsequent
/// candidates — across strategies and across lines of the same draft — see the
/// space as taken. Nothing here touches the database.
/// </summary>
public sealed class PutawayPlanContext
{
    private readonly Dictionary<Guid, Location> _locations;
    private readonly Dictionary<Guid, Product> _productsById;
    private readonly Dictionary<Guid, List<Inventory>> _contentsByLocation;
    private readonly Dictionary<Guid, CapacityOccupancy> _occupancyByLocation = [];

    public PutawayPlanContext(
        IEnumerable<Location> locations,
        IEnumerable<Inventory> inventories,
        IEnumerable<CapacityReservation> activeReservations,
        IEnumerable<Product> products)
    {
        _locations = locations.ToDictionary(l => l.Id);
        _productsById = products.ToDictionary(p => p.Id);

        _contentsByLocation = inventories
            .GroupBy(i => i.LocationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Other stock-ins' active reservations already occupy space on every dimension,
        // and their SKU/lot must be respected by mixed-SKU / mixed-lot bins.
        foreach (var reservation in activeReservations)
            if (_productsById.TryGetValue(reservation.ProductId, out var product))
                OccupancyFor(reservation.LocationId)
                    .Add(
                        CapacityLoadCalculator.Load(product, reservation.Quantity),
                        reservation.ProductId,
                        reservation.LotId);
    }

    public IReadOnlyCollection<Location> Locations => _locations.Values;

    /// <summary>Products referenced by the snapshot's inventory and reservations, by id.</summary>
    public IReadOnlyDictionary<Guid, Product> Products => _productsById;

    public Location? GetLocation(Guid locationId) =>
        _locations.TryGetValue(locationId, out var location) ? location : null;

    public IReadOnlyCollection<Inventory> ContentsAt(Guid locationId) =>
        _contentsByLocation.TryGetValue(locationId, out var list) ? list : [];

    /// <summary>
    /// Units already taken up at a location: on-hand inventory plus any capacity
    /// occupied by other stock-ins' active reservations or sibling placements
    /// committed earlier in this draft. Read-only — unlike <see cref="OccupancyFor"/>
    /// it never materialises an occupancy entry, so strategies can stay side-effect free.
    /// </summary>
    public int OccupiedUnits(Guid locationId) =>
        ContentsAt(locationId).Sum(i => i.OnHand.Value)
        + (int)(_occupancyByLocation.TryGetValue(locationId, out var occupancy)
            ? occupancy.Get(CapacityDimension.Units)
            : 0);

    public CapacityOccupancy OccupancyFor(Guid locationId)
    {
        if (!_occupancyByLocation.TryGetValue(locationId, out var occupancy))
        {
            occupancy = new CapacityOccupancy();
            _occupancyByLocation[locationId] = occupancy;
        }

        return occupancy;
    }

    /// <summary>Records that <paramref name="quantity"/> of <paramref name="product"/> (optionally <paramref name="lot"/>) has been planned into a location.</summary>
    public void Commit(Guid locationId, Product product, Lot? lot, Quantity quantity)
    {
        // The occupancy carries the planned SKU/lot identity, so a later candidate's
        // Location.CanAccept sees this sibling and enforces mixed-SKU / mixed-lot itself.
        OccupancyFor(locationId).Add(CapacityLoadCalculator.Load(product, quantity), product.Id, lot?.Id);
    }
}
