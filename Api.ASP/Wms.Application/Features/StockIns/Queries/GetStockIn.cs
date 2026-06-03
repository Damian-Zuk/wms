using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Queries;

public sealed record StockInPlacementDto(
    Guid Id,
    LocationRef Location,
    int Quantity,
    PutawayStrategyType Strategy);

public sealed record StockInLineDto(
    Guid Id,
    ProductRef Product,
    LotRef? Lot,
    int Quantity,
    IReadOnlyList<StockInPlacementDto> Placements);

public sealed record StockInDto(
    Guid Id,
    StockInStatus Status,
    DateTime CreatedAt,
    string? CreatedBy,
    string? ModifiedBy,
    DateTime? ModifiedAt,
    IReadOnlyList<StockInLineDto> Lines);

public sealed record GetStockInQuery(Guid Id) : IQuery<StockInDto>;

public sealed class GetStockInQueryHandler(IAppDbContext context)
    : IQueryHandler<GetStockInQuery, StockInDto>
{
    public async Task<Result<StockInDto>> Handle(GetStockInQuery query, CancellationToken cancellationToken)
    {
        var stockIn = await context.StockIns
            .AsNoTracking()
            .Where(s => s.Id == query.Id)
            .Select(s => new
            {
                s.Id,
                s.Status,
                s.CreatedAt,
                s.CreatedBy,
                s.ModifiedBy,
                s.ModifiedAt,
                Lines = s.Lines.Select(l => new
                {
                    l.Id,
                    l.ProductId,
                    l.LotId,
                    Quantity = l.Quantity.Value,
                    Items = l.Items.OrderBy(i => i.Strategy).Select(i => new
                    {
                        i.Id,
                        i.LocationId,
                        Quantity = i.Quantity.Value,
                        i.Strategy
                    }).ToList()
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stockIn is null)
            return StockInErrors.NotFound(query.Id);

        var productIds = stockIn.Lines.Select(l => l.ProductId).Distinct().ToList();
        var lotIds = stockIn.Lines.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();
        var locationIds = stockIn.Lines.SelectMany(l => l.Items).Select(i => i.LocationId).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var lines = stockIn.Lines
            .Select(l => new StockInLineDto(
                l.Id,
                products[l.ProductId],
                l.LotId.HasValue ? lots[l.LotId.Value] : null,
                l.Quantity,
                l.Items
                    .Select(i => new StockInPlacementDto(i.Id, locations[i.LocationId], i.Quantity, i.Strategy))
                    .ToList()))
            .ToList();

        return new StockInDto(
            stockIn.Id,
            stockIn.Status,
            stockIn.CreatedAt,
            stockIn.CreatedBy,
            stockIn.ModifiedBy,
            stockIn.ModifiedAt,
            lines);
    }
}
