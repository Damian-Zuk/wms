using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockMovements.Queries;

public sealed record StockMovementDto(
    Guid Id,
    ProductRef Product,
    LocationRef Location,
    LotRef? Lot,
    int QuantityChange,
    StockMovementType Type,
    StockMovementSource Source,
    Guid SourceId,
    DateTime CreatedAt);

public sealed record GetStockMovementQuery(Guid Id) : IQuery<StockMovementDto>;

public sealed class GetStockMovementQueryHandler(IAppDbContext context)
    : IQueryHandler<GetStockMovementQuery, StockMovementDto>
{
    public async Task<Result<StockMovementDto>> Handle(
        GetStockMovementQuery query,
        CancellationToken cancellationToken)
    {
        var movement = await context.StockMovements
            .AsNoTracking()
            .Where(m => m.Id == query.Id)
            .Select(m => new
            {
                m.Id,
                m.ProductId,
                m.LocationId,
                m.LotId,
                m.QuantityChange,
                m.Type,
                m.Source,
                m.SourceId,
                m.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (movement is null)
            return StockMovementErrors.NotFound(query.Id);

        var products = await RefLookup.LoadProductRefsAsync(
            context, [movement.ProductId], cancellationToken);

        var locations = await RefLookup.LoadLocationRefsAsync(
            context, [movement.LocationId], cancellationToken);

        var lots = movement.LotId.HasValue
            ? await RefLookup.LoadLotRefsAsync(context, [movement.LotId.Value], cancellationToken)
            : [];

        return new StockMovementDto(
            movement.Id,
            products[movement.ProductId],
            locations[movement.LocationId],
            movement.LotId.HasValue ? lots[movement.LotId.Value] : null,
            movement.QuantityChange,
            movement.Type,
            movement.Source,
            movement.SourceId,
            movement.CreatedAt);
    }
}
