using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Extensions;
using Wms.Shared.Common;

namespace Wms.Application.Features.Locations.Queries;

public sealed record ListLocationsQuery(
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<LocationDto>>;

public sealed class ListLocationsValidator : AbstractValidator<ListLocationsQuery>
{
    public ListLocationsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListLocationsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListLocationsQuery, PagedResult<LocationDto>>
{
    public async Task<Result<PagedResult<LocationDto>>> Handle(
        ListLocationsQuery query,
        CancellationToken cancellationToken)
    {
        var locationsQuery = context.Locations
            .AsNoTracking().AsQueryable().ApplyIsDeletedFilter();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            locationsQuery = locationsQuery.Where(l =>
                l.Code.Value.Contains(term) ||
                (l.Description != null && l.Description.Contains(term)));
        }

        var totalCount = await locationsQuery.CountAsync(cancellationToken);

        var items = await locationsQuery
            .OrderBy(l => l.Code.Value)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => new LocationDto(l.Id, l.Code.Value, l.Description))
            .ToListAsync(cancellationToken);

        return new PagedResult<LocationDto>(items, query.Page, query.PageSize, totalCount);
    }
}
