using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
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
        var stockOutsQuery = context.StockOuts
            .AsNoTracking()
            .Include(s => s.Items)
            .AsQueryable();

        var totalCount = await stockOutsQuery.CountAsync(cancellationToken);

        var items = await stockOutsQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new StockOutDto(
                s.Id,
                s.CreatedAt,
                s.CreatedBy,
                s.Items.Select(i => new StockOutItemDto(i.Id, i.ProductId, i.LocationId, i.LotId, i.Quantity.Value)).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<StockOutDto>(items, query.Page, query.PageSize, totalCount);
    }
}
