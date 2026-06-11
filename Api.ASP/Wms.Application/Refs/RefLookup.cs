using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;

namespace Wms.Application.Refs;

/// <summary>
/// Helpers that load <see cref="ProductRef"/>, <see cref="LocationRef"/>,
/// and <see cref="LotRef"/> dictionaries for a given set of IDs. Used by
/// list and detail handlers to populate embedded refs in a single round
/// trip per kind.
/// </summary>
public static class RefLookup
{
    public static async Task<Dictionary<Guid, ProductRef>> LoadProductRefsAsync(
        IAppDbContext context,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, ProductRef>();

        return await context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new ProductRef(p.Id, p.Sku.Value, p.Name, p.UnitPrice))
            .ToDictionaryAsync(r => r.Id, cancellationToken);
    }

    public static async Task<Dictionary<Guid, LocationRef>> LoadLocationRefsAsync(
        IAppDbContext context,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, LocationRef>();

        return await context.Locations
            .AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new LocationRef(
                l.Id,
                l.Code.Value,
                l.Address.ToString()))
            .ToDictionaryAsync(r => r.Id, cancellationToken);
    }

    public static async Task<Dictionary<Guid, LotRef>> LoadLotRefsAsync(
        IAppDbContext context,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, LotRef>();

        return await context.Lots
            .AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new LotRef(l.Id, l.Number.Value, l.ExpirationDate))
            .ToDictionaryAsync(r => r.Id, cancellationToken);
    }
}
