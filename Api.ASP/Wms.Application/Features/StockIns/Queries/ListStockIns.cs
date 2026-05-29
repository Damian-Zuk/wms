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
                Items = s.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    i.LocationId,
                    i.LotId,
                    Quantity = i.Quantity.Value
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var allItems = page.SelectMany(s => s.Items).ToList();
        var productIds = allItems.Select(i => i.ProductId).Distinct().ToList();
        var locationIds = allItems.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = allItems.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var items = page.Select(s => new StockInDto(
                s.Id,
                s.Status,
                s.CreatedAt,
                s.CreatedBy,
                s.Items.Select(i => new StockInItemDto(
                        i.Id,
                        products[i.ProductId],
                        locations[i.LocationId],
                        i.LotId.HasValue ? lots[i.LotId.Value] : null,
                        i.Quantity))
                    .ToList()))
            .ToList();

        return new PagedResult<StockInDto>(items, query.Page, query.PageSize, totalCount);
    }
}
