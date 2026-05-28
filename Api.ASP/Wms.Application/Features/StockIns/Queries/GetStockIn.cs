using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Dtos;
using Wms.Application.Common.Interfaces;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Queries;

public sealed record StockInItemDto(
    Guid Id,
    ProductRef Product,
    LocationRef Location,
    LotRef? Lot,
    int Quantity);

public sealed record StockInDto(
    Guid Id,
    StockInStatus Status,
    DateTime CreatedAt,
    string? CreatedBy,
    IReadOnlyList<StockInItemDto> Items);

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
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.CreatedAt,
                s.CreatedBy,
                Items = s.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    i.LocationId,
                    i.LotId,
                    Quantity = i.Quantity.Value
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(query.Id);

        var productIds = stockIn.Items.Select(i => i.ProductId).Distinct().ToList();
        var locationIds = stockIn.Items.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = stockIn.Items.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var items = stockIn.Items
            .Select(i => new StockInItemDto(
                i.Id,
                products[i.ProductId],
                locations[i.LocationId],
                i.LotId.HasValue ? lots[i.LotId.Value] : null,
                i.Quantity))
            .ToList();

        return new StockInDto(
            stockIn.Id,
            stockIn.Status,
            stockIn.CreatedAt,
            stockIn.CreatedBy,
            items);
    }
}
