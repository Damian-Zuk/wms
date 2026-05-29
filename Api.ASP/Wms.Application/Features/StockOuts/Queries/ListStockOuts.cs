using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Refs;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockOuts.Queries;

public sealed record ListStockOutsQuery(
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<StockOutDto>>;

public sealed class ListStockOutsValidator : AbstractValidator<ListStockOutsQuery>
{
    public ListStockOutsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListStockOutsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListStockOutsQuery, PagedResult<StockOutDto>>
{
    public async Task<Result<PagedResult<StockOutDto>>> Handle(
        ListStockOutsQuery query,
        CancellationToken cancellationToken)
    {
        var stockOutsQuery = context.StockOuts.AsNoTracking().AsQueryable();

        var totalCount = await stockOutsQuery.CountAsync(cancellationToken);

        var page = await stockOutsQuery
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

        var items = page.Select(s => new StockOutDto(
                s.Id,
                s.Status,
                s.CreatedAt,
                s.CreatedBy,
                s.Items.Select(i => new StockOutItemDto(
                        i.Id,
                        products[i.ProductId],
                        locations[i.LocationId],
                        i.LotId.HasValue ? lots[i.LotId.Value] : null,
                        i.Quantity))
                    .ToList()))
            .ToList();

        return new PagedResult<StockOutDto>(items, query.Page, query.PageSize, totalCount);
    }
}
