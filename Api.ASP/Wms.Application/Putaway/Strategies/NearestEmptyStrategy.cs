using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;

namespace Wms.Application.Putaway.Strategies;

/// <summary>
/// Suggests the "nearest empty" Storage location whose temperature zone
/// matches the product. Empty means zero Inventory rows for that location
/// (truly empty — not just zero-quantity). Ordering convention: ascending
/// LocationAddress (Zone → Aisle → Rack → Shelf → Bin, lexical per
/// segment). "Lower address = closer to dock" is the assumed warehouse
/// layout convention, so the earliest address is preferred.
/// </summary>
internal sealed class NearestEmptyStrategy(IAppDbContext context) : IPutawayStrategy
{
    public string Name => "NearestEmpty";

    public async Task<PutawaySuggestion?> SuggestAsync(
        Product product,
        Lot? lot,
        Quantity quantity,
        CancellationToken ct)
    {
        var requiredZone = product.RequiredTemperatureZone;

        // Empty = no Inventory rows for that LocationId. We use a NOT EXISTS
        // subquery so a location with zero-quantity rows is NOT considered
        // empty.
        var candidates = await context.Locations
            .Where(l =>
                l.Type == LocationType.Storage &&
                l.IsActive &&
                !l.IsBlocked &&
                l.TemperatureZone == requiredZone &&
                !context.Inventories.Any(i => i.LocationId == l.Id))
            .OrderBy(l => l.Address.Zone)
                .ThenBy(l => l.Address.Aisle)
                .ThenBy(l => l.Address.Rack)
                .ThenBy(l => l.Address.Shelf)
                .ThenBy(l => l.Address.Bin)
            .ToListAsync(ct);

        foreach (var location in candidates)
        {
            // Empty by definition, but pass empty contents for completeness
            // and to keep the contract uniform with the other strategies.
            var canAccept = location.CanAccept(
                product,
                lot,
                quantity,
                Array.Empty<Inventory>());

            if (canAccept.IsSuccess)
                return new PutawaySuggestion(location.Id, Name);
        }

        return null;
    }
}
