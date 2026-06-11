using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Extensions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Locations.Queries;

public sealed record ListLocationsQuery(
    string? Search,
    string? Zone,
    LocationType? Type,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20,
    TemperatureZone? TemperatureZone = null,
    string? Status = null) : IQuery<PagedResult<LocationDto>>;

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

        if (query.TemperatureZone.HasValue)
        {
            locationsQuery = locationsQuery.Where(l => l.TemperatureZone == query.TemperatureZone.Value);
        }

        locationsQuery = query.Status?.Trim().ToLowerInvariant() switch
        {
            "blocked" => locationsQuery.Where(l => l.IsBlocked),
            "active" => locationsQuery.Where(l => !l.IsBlocked && l.IsActive),
            "inactive" => locationsQuery.Where(l => !l.IsBlocked && !l.IsActive),
            _ => locationsQuery,
        };

        var totalCount = await locationsQuery.CountAsync(cancellationToken);

        var desc = query.SortDescending;
        locationsQuery = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "code" => locationsQuery.OrderByDirection(l => l.Code.Value, desc),
            "type" => locationsQuery.OrderByDirection(l => l.Type, desc),
            "temperaturezone" => locationsQuery.OrderByDirection(l => l.TemperatureZone, desc),
            "capacity" => locationsQuery.OrderByDirection(CapacityFillRatio(), desc),
            "address" => OrderByAddress(locationsQuery, desc),
            _ => OrderByAddress(locationsQuery, false),
        };

        var items = await locationsQuery
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
                context.Inventories
                    .Where(i => i.LocationId == l.Id)
                    .Sum(i => i.OnHand.Value),
                l.Capacity.MaxWeight,
                context.Inventories
                    .Where(i => i.LocationId == l.Id)
                    .Join(context.Products, i => i.ProductId, p => p.Id, (i, p) => i.OnHand.Value * p.Weight)
                    .Sum(),
                l.Capacity.MaxVolume,
                context.Inventories
                    .Where(i => i.LocationId == l.Id)
                    .Join(context.Products, i => i.ProductId, p => p.Id, (i, p) => i.OnHand.Value * p.Volume)
                    .Sum(),
                l.IsMixedSkuAllowed,
                l.IsMixedLotAllowed,
                l.IsActive,
                l.IsBlocked,
                l.BlockedReason,
                l.Description))
            .ToListAsync(cancellationToken);

        return new PagedResult<LocationDto>(items, query.Page, query.PageSize, totalCount);
    }

    private static IOrderedQueryable<Location> OrderByAddress(IQueryable<Location> source, bool descending)
        => source
            .OrderByDirection(l => l.Address.Zone, descending)
            .ThenByDirection(l => l.Address.Aisle, descending)
            .ThenByDirection(l => l.Address.Rack, descending)
            .ThenByDirection(l => l.Address.Shelf, descending)
            .ThenByDirection(l => l.Address.Bin, descending);

    /// <summary>
    /// Sort key for "capacity": the location's overall fullness, taken as the
    /// occupancy ratio (occupied / limit) of the most restrictive configured
    /// dimension. Dimensions with no limit contribute 0, so a fully unlimited 
    /// location sorts as 0% (least full).
    /// </summary>
    private Expression<Func<Location, decimal>> CapacityFillRatio()
        => l => Math.Max(
            l.Capacity.MaxUnits != null && l.Capacity.MaxUnits > 0
                ? (decimal)context.Inventories
                    .Where(i => i.LocationId == l.Id)
                    .Sum(i => i.OnHand.Value) / l.Capacity.MaxUnits.Value
                : 0m,
            Math.Max(
                l.Capacity.MaxWeight != null && l.Capacity.MaxWeight > 0
                    ? context.Inventories
                        .Where(i => i.LocationId == l.Id)
                        .Join(context.Products, i => i.ProductId, p => p.Id, (i, p) => i.OnHand.Value * p.Weight)
                        .Sum() / l.Capacity.MaxWeight.Value
                    : 0m,
                l.Capacity.MaxVolume != null && l.Capacity.MaxVolume > 0
                    ? context.Inventories
                        .Where(i => i.LocationId == l.Id)
                        .Join(context.Products, i => i.ProductId, p => p.Id, (i, p) => i.OnHand.Value * p.Volume)
                        .Sum() / l.Capacity.MaxVolume.Value
                    : 0m));
}
