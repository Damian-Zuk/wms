using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Walks the product's preferred locations (in Sequence order) and returns
/// the first one that accepts the incoming putaway. Returns null if the
/// product has no preferred locations or none of them accept — the next
/// strategy in the chain gets a chance.
/// </summary>
internal sealed class FixedLocationStrategy(IAppDbContext context) : IPutawayStrategy
{
    public string Name => "FixedLocation";

    public async Task<PutawaySuggestion?> SuggestAsync(
        Product product,
        Lot? lot,
        Quantity quantity,
        CancellationToken ct)
    {
        var preferredLocationIds = await context.ProductPreferredLocations
            .AsNoTracking()
            .Where(pl => pl.ProductId == product.Id)
            .OrderBy(pl => pl.Sequence)
            .Select(pl => pl.LocationId)
            .ToListAsync(ct);

        if (preferredLocationIds.Count == 0)
            return null;

        // Pre-load the candidate locations and all their current contents in
        // two round trips, then iterate in Sequence order.
        var locations = (await context.Locations
                .Where(l => preferredLocationIds.Contains(l.Id))
                .ToListAsync(ct))
            .ToDictionary(l => l.Id);

        var contentsByLocation = (await context.Inventories
                .AsNoTracking()
                .Where(i => preferredLocationIds.Contains(i.LocationId))
                .ToListAsync(ct))
            .GroupBy(i => i.LocationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var locationId in preferredLocationIds)
        {
            if (!locations.TryGetValue(locationId, out var location))
                continue;

            var contents = contentsByLocation.TryGetValue(locationId, out var list)
                ? list
                : new List<Inventory>();

            var canAccept = location.CanAccept(product, lot, quantity, contents);
            if (canAccept.IsSuccess)
                return new PutawaySuggestion(location.Id, Name);
        }

        return null;
    }
}
