using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Ranks locations by how close they sit to existing stock of the same product —
/// matched by lot when the incoming line carries one, falling back to SKU when the
/// warehouse holds no other stock of that lot yet (a lotless line always matches by
/// SKU). Proximity radiates outward from each such "anchor" location's address:
/// ascending before descending within a level, widening Bin → Shelf → Rack → Aisle →
/// Zone. Each candidate is keyed against its NEAREST anchor; the anchor locations
/// themselves are excluded since the consolidation strategies already target those.
/// </summary>
internal sealed class ProximityAllocationStrategy : IPutawayAllocationStrategy
{
    private const int Ascending = 0;
    private const int Descending = 1;

    public PutawayStrategyType Type => PutawayStrategyType.Proximity;

    public IReadOnlyList<Guid> CandidateLocations(Product product, Lot? lot, PutawayPlanContext context)
    {
        var anchors = Anchors(product, lot, context);
        if (anchors.Count == 0)
            return [];

        var anchorIds = anchors.Select(a => a.Id).ToHashSet();

        return context.Locations
            .Where(l => !anchorIds.Contains(l.Id))
            .Select(l => (l.Id, Key: NearestProximity(l.Address, anchors)))
            .Where(x => x.Key is not null)
            .OrderBy(x => x.Key!.Value)
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>
    /// Locations already holding the same lot; or, when none do (or the line has no
    /// lot), locations holding the same SKU.
    /// </summary>
    private static IReadOnlyList<Location> Anchors(Product product, Lot? lot, PutawayPlanContext context)
    {
        if (lot is not null)
        {
            var sameLot = Holders(context, i => i.ProductId == product.Id && i.LotId == lot.Id);
            if (sameLot.Count > 0)
                return sameLot;
        }

        return Holders(context, i => i.ProductId == product.Id);
    }

    private static List<Location> Holders(PutawayPlanContext context, Func<Inventory, bool> match) =>
        context.Locations
            .Where(l => context.ContentsAt(l.Id).Any(match))
            .ToList();

    private static ProximityKey? NearestProximity(LocationAddress address, IReadOnlyList<Location> anchors)
    {
        ProximityKey? nearest = null;

        foreach (var anchor in anchors)
        {
            var key = Proximity(address, anchor.Address);
            if (key is not null && (nearest is null || key.Value.CompareTo(nearest.Value) < 0))
                nearest = key;
        }

        return nearest;
    }

    /// <summary>
    /// Proximity of <paramref name="candidate"/> to <paramref name="anchor"/>: the
    /// coarsest segment at which they diverge sets the level (Zone widest, Bin
    /// nearest), and whether the candidate sorts after the anchor sets the direction.
    /// Null when the two addresses are identical.
    /// </summary>
    private static ProximityKey? Proximity(LocationAddress candidate, LocationAddress anchor)
    {
        if (candidate.Zone != anchor.Zone) return Key(level: 5, candidate.Zone, anchor.Zone, candidate);
        if (candidate.Aisle != anchor.Aisle) return Key(level: 4, candidate.Aisle, anchor.Aisle, candidate);
        if (candidate.Rack != anchor.Rack) return Key(level: 3, candidate.Rack, anchor.Rack, candidate);
        if (candidate.Shelf != anchor.Shelf) return Key(level: 2, candidate.Shelf, anchor.Shelf, candidate);
        if (candidate.Bin != anchor.Bin) return Key(level: 1, candidate.Bin, anchor.Bin, candidate);
        return null;
    }

    private static ProximityKey Key(int level, string candidateSegment, string anchorSegment, LocationAddress address)
    {
        var direction = string.CompareOrdinal(candidateSegment, anchorSegment) > 0 ? Ascending : Descending;
        return new ProximityKey(level, direction, candidateSegment, address);
    }

    /// <summary>
    /// Sort key placing nearer candidates first: by level (Bin = 1 first), then
    /// ascending before descending, then by segment distance from the anchor
    /// (nearest first in each direction), with the full address as a stable tiebreak.
    /// </summary>
    private readonly record struct ProximityKey(int Level, int Direction, string Segment, LocationAddress Address)
        : IComparable<ProximityKey>
    {
        public int CompareTo(ProximityKey other)
        {
            if (Level != other.Level) return Level.CompareTo(other.Level);
            if (Direction != other.Direction) return Direction.CompareTo(other.Direction);

            // Same level and direction: order by distance from the anchor. Ascending
            // wants the smaller segment first (just above the anchor); descending wants
            // the larger segment first (just below it).
            var segmentCmp = string.CompareOrdinal(Segment, other.Segment);
            if (segmentCmp != 0)
                return Direction == Ascending ? segmentCmp : -segmentCmp;

            return Address.CompareTo(other.Address);
        }
    }
}
