using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockMovements.Queries;

public sealed record StockMovementDto(
    Guid Id,
    Guid ProductId,
    Guid LocationId,
    Guid? LotId,
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
            .Select(m => new StockMovementDto(
                m.Id,
                m.ProductId,
                m.LocationId,
                m.LotId,
                m.QuantityChange,
                m.Type,
                m.Source,
                m.SourceId,
                m.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return movement is null ? Error.NotFound : movement;
    }
}
