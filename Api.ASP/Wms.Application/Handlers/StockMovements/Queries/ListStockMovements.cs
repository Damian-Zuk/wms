using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.StockMovements.Queries;

public sealed record ListStockMovementsQuery(
    Guid? ProductId,
    Guid? LocationId,
    Guid? LotId,
    StockMovementType? Type,
    StockMovementSource? Source,
    int Page = 1,
    int PageSize = 20,
    Guid? HandlingUnitId = null) : IQuery<PagedResult<StockMovementDto>>;


public sealed class ListStockMovementsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListStockMovementsQuery, PagedResult<StockMovementDto>>
{
    public async Task<Result<PagedResult<StockMovementDto>>> Handle(
        ListStockMovementsQuery query,
        CancellationToken cancellationToken)
    {
        var movementsQuery = context.StockMovements.AsNoTracking().AsQueryable();

        if (query.ProductId.HasValue)
            movementsQuery = movementsQuery.Where(m => m.ProductId == query.ProductId.Value);

        if (query.LocationId.HasValue)
            movementsQuery = movementsQuery.Where(m => m.LocationId == query.LocationId.Value);

        if (query.LotId.HasValue)
            movementsQuery = movementsQuery.Where(m => m.LotId == query.LotId.Value);

        if (query.Type.HasValue)
            movementsQuery = movementsQuery.Where(m => m.Type == query.Type.Value);

        if (query.Source.HasValue)
            movementsQuery = movementsQuery.Where(m => m.Source == query.Source.Value);

        if (query.HandlingUnitId.HasValue)
            movementsQuery = movementsQuery.Where(m => m.HandlingUnitId == query.HandlingUnitId.Value);

        var totalCount = await movementsQuery.CountAsync(cancellationToken);

        var page = await movementsQuery
            .OrderByDescending(m => m.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(m => new
            {
                m.Id,
                m.ProductId,
                m.LocationId,
                m.LotId,
                m.HandlingUnitId,
                m.QuantityChange,
                m.Type,
                m.Source,
                m.SourceId,
                m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var productIds = page.Select(m => m.ProductId).Distinct().ToList();
        var locationIds = page.Select(m => m.LocationId).Distinct().ToList();
        var lotIds = page.Where(m => m.LotId.HasValue).Select(m => m.LotId!.Value).Distinct().ToList();
        var handlingUnitIds = page
            .Where(m => m.HandlingUnitId.HasValue)
            .Select(m => m.HandlingUnitId!.Value)
            .Distinct()
            .ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);
        var handlingUnits = await RefLookup.LoadHandlingUnitRefsAsync(context, handlingUnitIds, cancellationToken);

        var items = page.Select(m => new StockMovementDto(
                m.Id,
                products[m.ProductId],
                locations[m.LocationId],
                m.LotId.HasValue ? lots[m.LotId.Value] : null,
                m.QuantityChange,
                m.Type,
                m.Source,
                m.SourceId,
                m.CreatedAt,
                m.HandlingUnitId.HasValue ? handlingUnits[m.HandlingUnitId.Value] : null))
            .ToList();

        return new PagedResult<StockMovementDto>(items, query.Page, query.PageSize, totalCount);
    }
}
