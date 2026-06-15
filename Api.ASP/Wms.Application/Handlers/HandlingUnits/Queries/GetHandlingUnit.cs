using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.HandlingUnits.Queries;

public sealed record HandlingUnitContentDto(
    Guid InventoryId,
    ProductRef Product,
    LotRef? Lot,
    int OnHand,
    int Reserved,
    int Available,
    DateTime? ReceivedAt);

public sealed record HandlingUnitDto(
    Guid Id,
    string Code,
    HandlingUnitType Type,
    LocationRef? Location,
    DateTime CreatedAt,
    string? CreatedBy,
    IReadOnlyList<HandlingUnitContentDto> Contents);

public sealed record GetHandlingUnitQuery(Guid Id) : IQuery<HandlingUnitDto>;

public sealed class GetHandlingUnitQueryHandler(IAppDbContext context)
    : IQueryHandler<GetHandlingUnitQuery, HandlingUnitDto>
{
    public async Task<Result<HandlingUnitDto>> Handle(GetHandlingUnitQuery query, CancellationToken cancellationToken)
    {
        var handlingUnit = await context.HandlingUnits
            .AsNoTracking()
            .Where(h => h.Id == query.Id)
            .Select(h => new
            {
                h.Id,
                Code = h.Code.Value,
                h.Type,
                h.LocationId,
                h.CreatedAt,
                h.CreatedBy
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (handlingUnit is null)
            return HandlingUnitErrors.NotFound(query.Id);

        var contents = await context.Inventories
            .AsNoTracking()
            .Where(i => i.HandlingUnitId == query.Id && (i.OnHand.Value > 0 || i.Reserved.Value > 0))
            .Select(i => new
            {
                i.Id,
                i.ProductId,
                i.LotId,
                OnHand = i.OnHand.Value,
                Reserved = i.Reserved.Value,
                i.ReceivedAt
            })
            .ToListAsync(cancellationToken);

        var productIds = contents.Select(c => c.ProductId).Distinct().ToList();
        var lotIds = contents.Where(c => c.LotId.HasValue).Select(c => c.LotId!.Value).Distinct().ToList();
        var locationIds = handlingUnit.LocationId.HasValue ? new[] { handlingUnit.LocationId.Value } : [];

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);

        return new HandlingUnitDto(
            handlingUnit.Id,
            handlingUnit.Code,
            handlingUnit.Type,
            handlingUnit.LocationId.HasValue ? locations[handlingUnit.LocationId.Value] : null,
            handlingUnit.CreatedAt,
            handlingUnit.CreatedBy,
            contents
                .Select(c => new HandlingUnitContentDto(
                    c.Id,
                    products[c.ProductId],
                    c.LotId.HasValue ? lots[c.LotId.Value] : null,
                    c.OnHand,
                    c.Reserved,
                    c.OnHand - c.Reserved,
                    c.ReceivedAt))
                .ToList());
    }
}
