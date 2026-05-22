using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Suggests a location that already holds the same product (and same lot
/// when one is supplied). Picks the location with the LEAST remaining
/// capacity — fill-up-first to maximise consolidation. Locations with
/// unlimited capacity are deprioritised (treated as infinite remaining
/// capacity) so finite-capacity locations get filled before unlimited
/// ones get touched. Tie-breaks by LocationAddress ascending for
/// determinism.
/// </summary>
internal sealed class ConsolidateSameSkuStrategy(IAppDbContext context) : IPutawayStrategy
{
    public string Name => "ConsolidateSameSku";

    public async Task<PutawaySuggestion?> SuggestAsync(
        Product product,
        Lot? lot,
        Quantity quantity,
        CancellationToken ct)
    {
        // Candidate locations: those that already hold the same product
        // (matching lot logic — null incoming lot matches only null-lot rows).
        var lotId = lot?.Id;

        var candidateLocationIds = await context.Inventories
            .AsNoTracking()
            .Where(i =>
                i.ProductId == product.Id &&
                i.LotId == lotId)
            .Select(i => i.LocationId)
            .Distinct()
            .ToListAsync(ct);

        if (candidateLocationIds.Count == 0)
            return null;

        var candidateLocations = await context.Locations
            .Where(l => candidateLocationIds.Contains(l.Id))
            .ToListAsync(ct);

        var contentsByLocation = (await context.Inventories
                .AsNoTracking()
                .Where(i => candidateLocationIds.Contains(i.LocationId))
                .ToListAsync(ct))
            .GroupBy(i => i.LocationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var survivors = new List<(Location Location, int CurrentTotal)>();

        foreach (var location in candidateLocations)
        {
            var contents = contentsByLocation.TryGetValue(location.Id, out var list)
                ? list
                : new List<Inventory>();

            var canAccept = location.CanAccept(product, lot, quantity, contents);
            if (canAccept.IsFailure)
                continue;

            var currentTotal = contents.Sum(i => i.Quantity.Value);
            survivors.Add((location, currentTotal));
        }

        if (survivors.Count == 0)
            return null;

        // Order so finite-capacity locations come before unlimited ones.
        // Within each group, prefer LEAST remaining capacity (fill-up-first).
        // For unlimited, "remaining" is treated as infinite; among them
        // we fall straight to the address tie-breaker.
        var chosen = survivors
            .OrderBy(s => s.Location.Capacity.HasValue ? 0 : 1)
            .ThenBy(s => s.Location.Capacity.HasValue
                ? s.Location.Capacity.Value - s.CurrentTotal
                : int.MaxValue)
            .ThenBy(s => s.Location.Address.Zone)
            .ThenBy(s => s.Location.Address.Aisle)
            .ThenBy(s => s.Location.Address.Rack)
            .ThenBy(s => s.Location.Address.Shelf)
            .ThenBy(s => s.Location.Address.Bin)
            .First();

        return new PutawaySuggestion(chosen.Location.Id, Name);
    }
}
