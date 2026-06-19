using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Refs;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockOuts.Queries;

public sealed record ListStockOutsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<StockOutDto>>;

public sealed class ListStockOutsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListStockOutsQuery, PagedResult<StockOutDto>>
{
    public async Task<Result<PagedResult<StockOutDto>>> Handle(
        ListStockOutsQuery query,
        CancellationToken cancellationToken)
    {
        var stockOutsQuery = context.StockOuts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            stockOutsQuery = stockOutsQuery.Where(s => s.Description != null && s.Description.ToLower().Contains(term));
        }

        var totalCount = await stockOutsQuery.CountAsync(cancellationToken);

        var page = await stockOutsQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
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
            .ToListAsync(cancellationToken);

        var allItems = page.SelectMany(s => s.Lines).SelectMany(l => l.Items).ToList();
        var productIds = page.SelectMany(s => s.Lines).Select(l => l.ProductId).Distinct().ToList();
        var locationIds = allItems.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = allItems.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var items = page.Select(s => new StockOutDto(
                s.Id,
                s.Status,
                s.CancelledFrom,
                s.Description,
                s.CreatedAt,
                s.CreatedBy,
                s.Lines.Select(l => new StockOutLineDto(
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
                    .ToList()))
            .ToList();

        return new PagedResult<StockOutDto>(items, query.Page, query.PageSize, totalCount);
    }
}
