using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Extensions;
using Wms.Shared.Common;

namespace Wms.Application.Features.Lots.Queries;

public sealed record ListLotsQuery(
    Guid? ProductId,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<LotDto>>;

public sealed class ListLotsValidator : AbstractValidator<ListLotsQuery>
{
    public ListLotsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListLotsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListLotsQuery, PagedResult<LotDto>>
{
    public async Task<Result<PagedResult<LotDto>>> Handle(
        ListLotsQuery query,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soonThreshold = today.AddDays(30);

        var lotsQuery = context.Lots.AsNoTracking().AsQueryable();

        if (query.ProductId.HasValue)
            lotsQuery = lotsQuery.Where(l => l.ProductId == query.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            lotsQuery = lotsQuery.Where(l => l.Number.Value.Contains(term));
        }

        var totalCount = await lotsQuery.CountAsync(cancellationToken);

        var items = await lotsQuery
            .OrderBy(l => l.Number.Value)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => new LotDto(
                l.Id,
                l.Number.Value,
                l.ProductId,
                l.ManufactureDate,
                l.ExpirationDate,
                l.ExpirationDate != null && l.ExpirationDate.Value < today,
                l.ExpirationDate != null && l.ExpirationDate.Value <= soonThreshold))
            .ToListAsync(cancellationToken);

        return new PagedResult<LotDto>(items, query.Page, query.PageSize, totalCount);
    }
}
