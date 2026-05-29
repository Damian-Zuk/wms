using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Queries;

public sealed record StockOutItemDto(
    Guid Id,
    ProductRef Product,
    LocationRef Location,
    LotRef? Lot,
    int Quantity);

public sealed record StockOutDto(
    Guid Id,
    StockOutStatus Status,
    DateTime CreatedAt,
    string? CreatedBy,
    IReadOnlyList<StockOutItemDto> Items);

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

        if (stockOut is null)
            return StockOutErrors.NotFound(query.Id);

        var productIds = stockOut.Items.Select(i => i.ProductId).Distinct().ToList();
        var locationIds = stockOut.Items.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = stockOut.Items.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var items = stockOut.Items
            .Select(i => new StockOutItemDto(
                i.Id,
                products[i.ProductId],
                locations[i.LocationId],
                i.LotId.HasValue ? lots[i.LotId.Value] : null,
                i.Quantity))
            .ToList();

        return new StockOutDto(
            stockOut.Id,
            stockOut.Status,
            stockOut.CreatedAt,
            stockOut.CreatedBy,
            items);
    }
}
