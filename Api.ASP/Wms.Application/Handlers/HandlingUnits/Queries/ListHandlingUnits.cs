using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Common.Extensions;
using Wms.Application.Refs;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.HandlingUnits.Queries;

public sealed record HandlingUnitListItemDto(
    Guid Id,
    string Code,
    HandlingUnitType Type,
    LocationRef? Location,
    int TotalOnHand,
    int ProductCount,
    DateTime CreatedAt);

public sealed record ListHandlingUnitsQuery(
    string? Search = null,
    Guid? LocationId = null,
    HandlingUnitType? Type = null,
    bool? IsEmpty = null,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<HandlingUnitListItemDto>>;

public sealed class ListHandlingUnitsValidator : AbstractValidator<ListHandlingUnitsQuery>
{
    public ListHandlingUnitsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListHandlingUnitsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListHandlingUnitsQuery, PagedResult<HandlingUnitListItemDto>>
{
    public async Task<Result<PagedResult<HandlingUnitListItemDto>>> Handle(
        ListHandlingUnitsQuery query,
        CancellationToken cancellationToken)
    {
        var unitsQuery = context.HandlingUnits.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            unitsQuery = unitsQuery.Where(h => h.Code.Value.ToLower().Contains(term));
        }

        if (query.LocationId.HasValue)
            unitsQuery = unitsQuery.Where(h => h.LocationId == query.LocationId.Value);

        if (query.Type.HasValue)
            unitsQuery = unitsQuery.Where(h => h.Type == query.Type.Value);

        if (query.IsEmpty.HasValue)
        {
            unitsQuery = query.IsEmpty.Value
                ? unitsQuery.Where(h => !context.Inventories.Any(i => i.HandlingUnitId == h.Id && i.OnHand.Value > 0))
                : unitsQuery.Where(h => context.Inventories.Any(i => i.HandlingUnitId == h.Id && i.OnHand.Value > 0));
        }

        var totalCount = await unitsQuery.CountAsync(cancellationToken);

        bool desc = query.SortDescending;
        unitsQuery = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "code" => unitsQuery.OrderByDirection(h => h.Code.Value, desc),
            "type" => unitsQuery.OrderByDirection(h => h.Type, desc),
            "location" => unitsQuery.OrderByDirection(
                h => context.Locations
                    .Where(l => l.Id == h.LocationId)
                    .Select(l => l.Address.ToString())
                    .FirstOrDefault(),
                desc),
            "onhand" => unitsQuery.OrderByDirection(
                h => context.Inventories
                    .Where(i => i.HandlingUnitId == h.Id)
                    .Sum(i => i.OnHand.Value),
                desc),
            "createdat" => unitsQuery.OrderByDirection(h => h.CreatedAt, desc),
            _ => unitsQuery.OrderByDirection(h => h.Code.Value, desc),
        };

        var page = await unitsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(h => new
            {
                h.Id,
                Code = h.Code.Value,
                h.Type,
                h.LocationId,
                h.CreatedAt,
                TotalOnHand = context.Inventories
                    .Where(i => i.HandlingUnitId == h.Id)
                    .Sum(i => (int?)i.OnHand.Value) ?? 0,
                ProductCount = context.Inventories
                    .Where(i => i.HandlingUnitId == h.Id && i.OnHand.Value > 0)
                    .Select(i => i.ProductId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken);

        var locationIds = page
            .Where(h => h.LocationId.HasValue)
            .Select(h => h.LocationId!.Value)
            .Distinct()
            .ToList();

        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);

        var items = page.Select(h => new HandlingUnitListItemDto(
                h.Id,
                h.Code,
                h.Type,
                h.LocationId.HasValue ? locations[h.LocationId.Value] : null,
                h.TotalOnHand,
                h.ProductCount,
                h.CreatedAt))
            .ToList();

        return new PagedResult<HandlingUnitListItemDto>(items, query.Page, query.PageSize, totalCount);
    }
}
