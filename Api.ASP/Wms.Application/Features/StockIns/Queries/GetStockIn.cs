using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Queries;

public sealed record StockInItemDto(Guid Id, Guid ProductId, Guid LocationId, Guid? LotId, int Quantity);

public sealed record StockInDto(Guid Id, DateTime CreatedAt, string? CreatedBy, List<StockInItemDto> Items);

public sealed record GetStockInQuery(Guid Id) : IQuery<StockInDto>;

public sealed class GetStockInQueryHandler(IAppDbContext context)
    : IQueryHandler<GetStockInQuery, StockInDto>
{
    public async Task<Result<StockInDto>> Handle(GetStockInQuery query, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Id == query.Id)
            .Select(s => new StockInDto(
                s.Id,
                s.CreatedAt,
                s.CreatedBy,
                s.Items.Select(i => new StockInItemDto(i.Id, i.ProductId, i.LocationId, i.LotId, i.Quantity.Value)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return stockIn is null ? Error.NotFound : stockIn;
    }
}
