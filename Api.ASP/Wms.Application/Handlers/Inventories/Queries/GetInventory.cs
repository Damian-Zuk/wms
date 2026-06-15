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
    DateOnly? ExpirationDate,
    int OnHand,
    int Reserved,
    int Available,
    decimal OnHandValue,
    HandlingUnitRef? HandlingUnit);

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
                i.HandlingUnitId,
                OnHand = i.OnHand.Value,
                Reserved = i.Reserved.Value,
                UnitPrice = context.Products
                    .Where(p => p.Id == i.ProductId)
                    .Select(p => p.UnitPrice)
                    .FirstOrDefault()
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

        var handlingUnits = inventory.HandlingUnitId.HasValue
            ? await RefLookup.LoadHandlingUnitRefsAsync(context, [inventory.HandlingUnitId.Value], cancellationToken)
            : new Dictionary<Guid, HandlingUnitRef>();

        return new InventoryDto(
            inventory.Id,
            products[inventory.ProductId],
            locations[inventory.LocationId],
            inventory.LotId.HasValue ? lots[inventory.LotId.Value] : null,
            inventory.LotId.HasValue ? lots[inventory.LotId.Value].ExpirationDate : null,
            inventory.OnHand,
            inventory.Reserved,
            inventory.OnHand - inventory.Reserved,
            inventory.OnHand * inventory.UnitPrice,
            inventory.HandlingUnitId.HasValue ? handlingUnits[inventory.HandlingUnitId.Value] : null);
    }
}
