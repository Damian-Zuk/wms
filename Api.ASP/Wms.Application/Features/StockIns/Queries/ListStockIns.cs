using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Refs;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockIns.Queries;

public sealed record ListStockInsQuery(
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<StockInDto>>;

public sealed class ListStockInsValidator : AbstractValidator<ListStockInsQuery>
{
    public ListStockInsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListStockInsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListStockInsQuery, PagedResult<StockInDto>>
{
    public async Task<Result<PagedResult<StockInDto>>> Handle(
        ListStockInsQuery query,
        CancellationToken cancellationToken)
    {
        var stockInsQuery = context.StockIns.AsNoTracking().AsQueryable();

        var totalCount = await stockInsQuery.CountAsync(cancellationToken);

        var page = await stockInsQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
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
                    Items = l.Items.Select(i => new
                    {
                        i.Id,
                        i.LocationId,
                        Quantity = i.Quantity.Value,
                        i.Strategy
                    }).ToList()
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var allLines = page.SelectMany(s => s.Lines).ToList();
        var productIds = allLines.Select(l => l.ProductId).Distinct().ToList();
        var lotIds = allLines.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();
        var locationIds = allLines.SelectMany(l => l.Items).Select(i => i.LocationId).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var items = page.Select(s => new StockInDto(
                s.Id,
                s.Status,
                s.CreatedAt,
                s.CreatedBy,
                s.ModifiedBy,
                s.ModifiedAt,
                s.Lines.Select(l => new StockInLineDto(
                        l.Id,
                        products[l.ProductId],
                        l.LotId.HasValue ? lots[l.LotId.Value] : null,
                        l.Quantity,
                        l.Items
                            .Select(i => new StockInPlacementDto(i.Id, locations[i.LocationId], i.Quantity, i.Strategy))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new PagedResult<StockInDto>(items, query.Page, query.PageSize, totalCount);
    }
}
