using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Features.Locations.Queries;

public sealed record ListLocationsQuery(
    string? Search,
    string? Zone,
    LocationType? Type,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<LocationDto>>;

public sealed class ListLocationsValidator : AbstractValidator<ListLocationsQuery>
{
    public ListLocationsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
        RuleFor(x => x.Type)
            .IsInEnum().When(x => x.Type.HasValue)
            .WithMessage("Type must be a valid LocationType");
    }
}

public sealed class ListLocationsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListLocationsQuery, PagedResult<LocationDto>>
{
    public async Task<Result<PagedResult<LocationDto>>> Handle(
        ListLocationsQuery query,
        CancellationToken cancellationToken)
    {
        var locationsQuery = context.Locations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            locationsQuery = locationsQuery.Where(l =>
                l.Code.Value.Contains(term) ||
                (l.Description != null && l.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Zone))
        {
            var zone = query.Zone.Trim();
            locationsQuery = locationsQuery.Where(l => l.Address.Zone == zone);
        }

        if (query.Type.HasValue)
        {
            locationsQuery = locationsQuery.Where(l => l.Type == query.Type.Value);
        }

        var totalCount = await locationsQuery.CountAsync(cancellationToken);

        var items = await locationsQuery
            .OrderBy(l => l.Address.Zone)
                .ThenBy(l => l.Address.Aisle)
                .ThenBy(l => l.Address.Rack)
                .ThenBy(l => l.Address.Shelf)
                .ThenBy(l => l.Address.Bin)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => new LocationDto(
                l.Id,
                l.Code.Value,
                new LocationAddressDto(
                    l.Address.Zone,
                    l.Address.Aisle,
                    l.Address.Rack,
                    l.Address.Shelf,
                    l.Address.Bin),
                l.Address.ToString(),
                l.Type,
                l.TemperatureZone,
                l.Capacity.MaxUnits,
                l.IsMixedSkuAllowed,
                l.IsMixedLotAllowed,
                l.IsActive,
                l.IsBlocked,
                l.BlockedReason,
                l.Description))
            .ToListAsync(cancellationToken);

        return new PagedResult<LocationDto>(items, query.Page, query.PageSize, totalCount);
    }
}
