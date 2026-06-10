using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Queries;

public sealed record StockOutItemDto(
    Guid Id,
    LocationRef Location,
    LotRef? Lot,
    int Quantity,
    int PickedQuantity,
    PickingStrategyType Strategy);

public sealed record StockOutLineDto(
    Guid Id,
    ProductRef Product,
    PickingStrategyType Strategy,
    int Quantity,
    IReadOnlyList<StockOutItemDto> Items);

public sealed record StockOutDto(
    Guid Id,
    StockOutStatus Status,
    StockOutStatus? CancelledFrom,
    string? Description,
    DateTime CreatedAt,
    string? CreatedBy,
    IReadOnlyList<StockOutLineDto> Lines);

public sealed record GetStockOutQuery(Guid Id) : IQuery<StockOutDto>;

public sealed class GetStockOutQueryHandler(IAppDbContext context)
    : IQueryHandler<GetStockOutQuery, StockOutDto>
{
    public async Task<Result<StockOutDto>> Handle(GetStockOutQuery query, CancellationToken cancellationToken)
    {
        var stockOut = await context.StockOuts
            .AsNoTracking()
            .Where(s => s.Id == query.Id)
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.CancelledFrom,
                s.Description,
                s.CreatedAt,
                s.CreatedBy,
                Lines = s.Lines.Select(l => new
                {
                    l.Id,
                    l.ProductId,
                    l.Strategy,
                    Quantity = l.Quantity.Value,
                    Items = l.Items.Select(i => new
                    {
                        i.Id,
                        i.LocationId,
                        i.LotId,
                        Quantity = i.Quantity.Value,
                        PickedQuantity = i.PickedQuantity.Value,
                        i.Strategy
                    }).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stockOut is null)
            return StockOutErrors.NotFound(query.Id);

        var productIds = stockOut.Lines.Select(l => l.ProductId).Distinct().ToList();
        var items = stockOut.Lines.SelectMany(l => l.Items).ToList();
        var locationIds = items.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = items.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var lines = stockOut.Lines
            .Select(l => new StockOutLineDto(
                l.Id,
                products[l.ProductId],
                l.Strategy,
                l.Quantity,
                l.Items
                    .Select(i => new StockOutItemDto(
                        i.Id,
                        locations[i.LocationId],
                        i.LotId.HasValue ? lots[i.LotId.Value] : null,
                        i.Quantity,
                        i.PickedQuantity,
                        i.Strategy))
                    .ToList()))
            .ToList();

        return new StockOutDto(
            stockOut.Id,
            stockOut.Status,
            stockOut.CancelledFrom,
            stockOut.Description,
            stockOut.CreatedAt,
            stockOut.CreatedBy,
            lines);
    }
}
