using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Queries;

public sealed record StockOutItemDto(Guid Id, Guid ProductId, Guid LocationId, Guid? LotId, int Quantity);

public sealed record StockOutDto(Guid Id, DateTime CreatedAt, string? CreatedBy, List<StockOutItemDto> Items);

public sealed record GetStockOutQuery(Guid Id) : IQuery<StockOutDto>;

public sealed class GetStockOutQueryHandler(IAppDbContext context)
    : IQueryHandler<GetStockOutQuery, StockOutDto>
{
    public async Task<Result<StockOutDto>> Handle(GetStockOutQuery query, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Id == query.Id)
            .Select(s => new StockOutDto(
                s.Id,
                s.CreatedAt,
                s.CreatedBy,
                s.Items.Select(i => new StockOutItemDto(i.Id, i.ProductId, i.LocationId, i.LotId, i.Quantity.Value)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return stockOut is null ? Error.NotFound : stockOut;
    }
}
