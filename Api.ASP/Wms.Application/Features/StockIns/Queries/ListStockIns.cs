using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
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
        var stockInsQuery = context.StockIns
            .AsNoTracking()
            .Include(s => s.Items)
            .AsQueryable();

        var totalCount = await stockInsQuery.CountAsync(cancellationToken);

        var items = await stockInsQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new StockInDto(
                s.Id,
                s.CreatedAt,
                s.CreatedBy,
                s.Items.Select(i => new StockInItemDto(i.Id, i.ProductId, i.LocationId, i.LotId, i.Quantity.Value)).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<StockInDto>(items, query.Page, query.PageSize, totalCount);
    }
}
