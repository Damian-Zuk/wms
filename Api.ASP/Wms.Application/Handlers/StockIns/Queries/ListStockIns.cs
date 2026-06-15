using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Refs;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockIns.Queries;

public sealed record ListStockInsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<StockInDto>>;

public sealed class ListStockInsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListStockInsQuery, PagedResult<StockInDto>>
{
    public async Task<Result<PagedResult<StockInDto>>> Handle(
        ListStockInsQuery query,
        CancellationToken cancellationToken)
    {
        var stockInsQuery = context.StockIns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            stockInsQuery = stockInsQuery.Where(s => s.Description != null && s.Description.ToLower().Contains(term));
        }

        var totalCount = await stockInsQuery.CountAsync(cancellationToken);

        var page = await stockInsQuery
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
                        PlacedQuantity = i.PlacedQuantity.Value,
                        i.Strategy,
                        i.HandlingUnitId
                    }).ToList()
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var allLines = page.SelectMany(s => s.Lines).ToList();
        var productIds = allLines.Select(l => l.ProductId).Distinct().ToList();
        var lotIds = allLines.Where(l => l.LotId.HasValue).Select(l => l.LotId!.Value).Distinct().ToList();
        var locationIds = allLines.SelectMany(l => l.Items).Select(i => i.LocationId).Distinct().ToList();
        var handlingUnitIds = allLines.SelectMany(l => l.Items)
            .Where(i => i.HandlingUnitId.HasValue)
            .Select(i => i.HandlingUnitId!.Value)
            .Distinct()
            .ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);
        var handlingUnits = await RefLookup.LoadHandlingUnitRefsAsync(context, handlingUnitIds, cancellationToken);

        var items = page.Select(s => new StockInDto(
                s.Id,
                s.Status,
                s.CancelledFrom,
                s.Description,
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
                            .Select(i => new StockInPlacementDto(
                                i.Id, locations[i.LocationId], i.Quantity, i.PlacedQuantity, i.Strategy,
                                i.HandlingUnitId.HasValue ? handlingUnits[i.HandlingUnitId.Value] : null))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new PagedResult<StockInDto>(items, query.Page, query.PageSize, totalCount);
    }
}
