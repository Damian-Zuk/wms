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
    private readonly Dictionary<Guid, List<Inventory>> _contentsByLocation;
    private readonly Dictionary<Guid, CapacityOccupancy> _occupancyByLocation = [];

    // SKUs / lots already planned into each location earlier in this draft.
    // CanAccept only sees committed inventory, but sibling placements aren't
    // inventory yet — these sets close the mixed-SKU / mixed-lot gap within a draft.
    private readonly Dictionary<Guid, HashSet<Guid>> _plannedProductsByLocation = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _plannedLotsByLocation = [];

    public PutawayPlanContext(
        IEnumerable<Location> locations,
        IEnumerable<Inventory> inventories,
        IEnumerable<CapacityReservation> activeReservations)
    {
        _locations = locations.ToDictionary(l => l.Id);

        _contentsByLocation = inventories
            .GroupBy(i => i.LocationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var group in activeReservations.GroupBy(r => r.LocationId))
        {
            OccupancyFor(group.Key).Add(new Dictionary<CapacityDimension, int>
            {
                [CapacityDimension.Units] = group.Sum(r => r.Quantity.Value)
            });
        }
    }

    public IReadOnlyCollection<Location> Locations => _locations.Values;

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
        + (_occupancyByLocation.TryGetValue(locationId, out var occupancy)
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
        OccupancyFor(locationId).Add(CapacityLoadCalculator.Load(product, quantity));
        PlannedProducts(locationId).Add(product.Id);

        // Lotless placements never trigger a mixed-lot conflict, mirroring CanAccept
        // which only flags inventory whose LotId.HasValue.
        if (lot is not null)
            PlannedLots(locationId).Add(lot.Id);
    }

    /// <summary>
    /// Whether placing <paramref name="product"/>/<paramref name="lot"/> into
    /// <paramref name="location"/> would mix with a different SKU or lot already
    /// planned into that same location earlier in this draft. <see cref="Location.CanAccept(Product, Lot?, Quantity, IEnumerable{Inventory}, CapacityOccupancy)"/>
    /// only sees committed inventory, so sibling placements must be checked here.
    /// </summary>
    public bool WouldConflictWithPlanned(Location location, Product product, Lot? lot)
    {
        if (!location.IsMixedSkuAllowed
            && PlannedProducts(location.Id).Any(id => id != product.Id))
            return true;

        if (!location.IsMixedLotAllowed
            && PlannedLots(location.Id).Any(id => id != lot?.Id))
            return true;

        return false;
    }

    private HashSet<Guid> PlannedProducts(Guid locationId)
    {
        if (!_plannedProductsByLocation.TryGetValue(locationId, out var set))
        {
            set = [];
            _plannedProductsByLocation[locationId] = set;
        }

        return set;
    }

    private HashSet<Guid> PlannedLots(Guid locationId)
    {
        if (!_plannedLotsByLocation.TryGetValue(locationId, out var set))
        {
            set = [];
            _plannedLotsByLocation[locationId] = set;
        }

        return set;
    }
}
