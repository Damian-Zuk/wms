using Wms.Domain.Entities;

namespace Wms.Application.Picking;

/// <summary>
/// A consistent in-memory snapshot the picking planner reasons over: every product's
/// available inventory sources (location + lot + handling unit, with units free and
/// the received date), plus the lots and locations needed to rank them. As the planner
/// allocates it <see cref="Commit"/>s the take back into the snapshot so sibling lines
/// of the same draft can't draw the same units twice. Nothing here touches the database.
/// </summary>
public sealed class PickingContext
{
    private sealed class Source(Guid locationId, Guid? lotId, Guid? handlingUnitId, int available, DateTime? receivedAt)
    {
        public Guid LocationId { get; } = locationId;
        public Guid? LotId { get; } = lotId;
        public Guid? HandlingUnitId { get; } = handlingUnitId;
        public int Available { get; set; } = available;
        public DateTime? ReceivedAt { get; } = receivedAt;
    }

    private readonly Dictionary<Guid, Dictionary<(Guid LocationId, Guid? LotId, Guid? HandlingUnitId), Source>> _byProduct = [];
    private readonly Dictionary<Guid, Lot> _lots;
    private readonly Dictionary<Guid, Location> _locations;

    public PickingContext(
        IEnumerable<Inventory> inventories,
        IEnumerable<Lot> lots,
        IEnumerable<Location> locations)
    {
        _lots = lots.ToDictionary(l => l.Id);
        _locations = locations.ToDictionary(l => l.Id);

        foreach (var inventory in inventories)
        {
            var available = inventory.Available.Value;
            if (available <= 0)
                continue;

            if (!_byProduct.TryGetValue(inventory.ProductId, out var sources))
            {
                sources = [];
                _byProduct[inventory.ProductId] = sources;
            }

            var key = (inventory.LocationId, inventory.LotId, inventory.HandlingUnitId);
            // Inventory is unique on (product, location, lot, hu); merge defensively anyway.
            if (sources.TryGetValue(key, out var existing))
                existing.Available += available;
            else
                sources[key] = new Source(
                    inventory.LocationId, inventory.LotId, inventory.HandlingUnitId, available, inventory.ReceivedAt);
        }
    }

    /// <summary>Available pick candidates for a product (only sources with units still free).</summary>
    public IReadOnlyList<PickCandidate> AvailableFor(Guid productId) =>
        _byProduct.TryGetValue(productId, out var sources)
            ? sources.Values
                .Where(s => s.Available > 0)
                .Select(s => new PickCandidate(s.LocationId, s.LotId, s.HandlingUnitId, s.Available, s.ReceivedAt))
                .ToList()
            : [];

    public Lot? GetLot(Guid? lotId) =>
        lotId.HasValue && _lots.TryGetValue(lotId.Value, out var lot) ? lot : null;

    public Location? GetLocation(Guid locationId) =>
        _locations.TryGetValue(locationId, out var location) ? location : null;

    /// <summary>Marks units at a product's source as taken so later lines don't reuse them.</summary>
    public void Commit(Guid productId, Guid locationId, Guid? lotId, Guid? handlingUnitId, int quantity)
    {
        if (_byProduct.TryGetValue(productId, out var sources)
            && sources.TryGetValue((locationId, lotId, handlingUnitId), out var source))
        {
            source.Available -= quantity;
        }
    }
}
