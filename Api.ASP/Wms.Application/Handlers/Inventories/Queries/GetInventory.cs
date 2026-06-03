using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Inventories.Queries;

public sealed record InventoryDto(
    Guid Id,
    ProductRef Product,
    LocationRef Location,
    LotRef? Lot,
    int OnHand,
    int Reserved,
    int Available);

public sealed record GetInventoryQuery(Guid Id) : IQuery<InventoryDto>;

public sealed class GetInventoryQueryHandler(IAppDbContext context)
    : IQueryHandler<GetInventoryQuery, InventoryDto>
{
    public async Task<Result<InventoryDto>> Handle(
        GetInventoryQuery query,
        CancellationToken cancellationToken)
    {
        var inventory = await context.Inventories
            .AsNoTracking()
            .Where(i => i.Id == query.Id)
            .Select(i => new
            {
                i.Id,
                i.ProductId,
                i.LocationId,
                i.LotId,
                OnHand = i.OnHand.Value,
                Reserved = i.Reserved.Value
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (inventory is null)
            return InventoryErrors.NotFound(query.Id);

        var products = await RefLookup.LoadProductRefsAsync(
            context, [inventory.ProductId], cancellationToken);

        var locations = await RefLookup.LoadLocationRefsAsync(
            context, [inventory.LocationId], cancellationToken);

        var lots = inventory.LotId.HasValue
            ? await RefLookup.LoadLotRefsAsync(context, [inventory.LotId.Value], cancellationToken)
            : new Dictionary<Guid, LotRef>();

        return new InventoryDto(
            inventory.Id,
            products[inventory.ProductId],
            locations[inventory.LocationId],
            inventory.LotId.HasValue ? lots[inventory.LotId.Value] : null,
            inventory.OnHand,
            inventory.Reserved,
            inventory.OnHand - inventory.Reserved);
    }
}
