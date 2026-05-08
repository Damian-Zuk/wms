using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Features.StockMovements.Queries;

public sealed record ListStockMovementsQuery(
    Guid? ProductId,
    Guid? LocationId,
    Guid? LotId,
    StockMovementType? Type,
    StockMovementSource? Source,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<StockMovementDto>>;

public sealed class ListStockMovementsValidator : AbstractValidator<ListStockMovementsQuery>
{
    public ListStockMovementsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListStockMovementsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListStockMovementsQuery, PagedResult<StockMovementDto>>
{
    public async Task<Result<PagedResult<StockMovementDto>>> Handle(
        ListStockMovementsQuery query,
        CancellationToken cancellationToken)
    {
        var movementsQuery = context.StockMovements
            .AsNoTracking().AsQueryable();

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

        var totalCount = await movementsQuery.CountAsync(cancellationToken);

        var items = await movementsQuery
            .OrderByDescending(m => m.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(m => new StockMovementDto(
                m.Id,
                m.ProductId,
                m.LocationId,
                m.LotId,
                m.QuantityChange,
                m.Type,
                m.Source,
                m.SourceId,
                m.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>(items, query.Page, query.PageSize, totalCount);
    }
}
