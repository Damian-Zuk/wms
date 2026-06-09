using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Extensions;
using Wms.Application.Handlers.ProductCategories;
using Wms.Application.Refs;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Inventories.Queries;

public sealed record ListInventoriesQuery(
    Guid? ProductId,
    Guid? LocationId,
    Guid? LotId,
    int? ExpiringWithinDays = null,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20,
    Guid? CategoryId = null) : IQuery<PagedResult<InventoryDto>>;

public sealed class ListInventoriesValidator : AbstractValidator<ListInventoriesQuery>
{
    public ListInventoriesValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListInventoriesQueryHandler(IAppDbContext context)
    : IQueryHandler<ListInventoriesQuery, PagedResult<InventoryDto>>
{
    public async Task<Result<PagedResult<InventoryDto>>> Handle(
        ListInventoriesQuery query,
        CancellationToken cancellationToken)
    {
        var inventoriesQuery = context.Inventories.AsNoTracking().AsQueryable();

        if (query.ProductId.HasValue)
            inventoriesQuery = inventoriesQuery.Where(i => i.ProductId == query.ProductId.Value);

        if (query.LocationId.HasValue)
            inventoriesQuery = inventoriesQuery.Where(i => i.LocationId == query.LocationId.Value);

        if (query.LotId.HasValue)
            inventoriesQuery = inventoriesQuery.Where(i => i.LotId == query.LotId.Value);

        if (query.ExpiringWithinDays.HasValue)
        {
            var threshold = DateOnly.FromDateTime(DateTime.Today).AddDays(query.ExpiringWithinDays.Value);
            inventoriesQuery = inventoriesQuery.Where(i =>
                context.Lots.Any(l =>
                    l.Id == i.LotId && l.ExpirationDate != null && l.ExpirationDate.Value <= threshold));
        }

        if (query.CategoryId.HasValue)
        {
            var hierarchy = await CategoryHierarchy.LoadAsync(context, cancellationToken);
            var subtree = hierarchy.DescendantIdsInclusive(query.CategoryId.Value).ToArray();
            inventoriesQuery = inventoriesQuery.Where(i => context.Products.Any(p =>
                p.Id == i.ProductId &&
                p.ProductCategoryId.HasValue &&
                subtree.Contains(p.ProductCategoryId.Value)));
        }

        var totalCount = await inventoriesQuery.CountAsync(cancellationToken);

        bool desc = query.SortDescending;
        inventoriesQuery = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "product" => inventoriesQuery.OrderByDirection(
                i => context.Products
                    .Where(p => p.Id == i.ProductId)
                    .Select(p => p.Sku.Value)
                    .FirstOrDefault(),
                desc),
            "location" => inventoriesQuery.OrderByDirection(
                i => context.Locations
                    .Where(l => l.Id == i.LocationId)
                    .Select(l => l.Address.ToString())
                    .FirstOrDefault(),
                desc),
            "lot" => inventoriesQuery.OrderByDirection(
                i => context.Lots
                    .Where(l => l.Id == i.LotId)
                    .Select(l => l.Number.Value)
                    .FirstOrDefault(),
                desc),
            "expirationdate" => inventoriesQuery.OrderByDirection(
                i => context.Lots
                    .Where(l => l.Id == i.LotId)
                    .Select(l => l.ExpirationDate)
                    .FirstOrDefault(),
                desc),
            "onhand" => inventoriesQuery.OrderByDirection(i => i.OnHand.Value, desc),
            "reserved" => inventoriesQuery.OrderByDirection(i => i.Reserved.Value, desc),
            "available" => inventoriesQuery.OrderByDirection(
                i => i.OnHand.Value - i.Reserved.Value, desc),
            _ => inventoriesQuery.OrderBy(i => i.ProductId),
        };

        var page = await inventoriesQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(i => new
            {
                i.Id,
                i.ProductId,
                i.LocationId,
                i.LotId,
                OnHand = i.OnHand.Value,
                Reserved = i.Reserved.Value
            })
            .ToListAsync(cancellationToken);

        var productIds = page.Select(i => i.ProductId).Distinct().ToList();
        var locationIds = page.Select(i => i.LocationId).Distinct().ToList();
        var lotIds = page.Where(i => i.LotId.HasValue).Select(i => i.LotId!.Value).Distinct().ToList();

        var products = await RefLookup.LoadProductRefsAsync(context, productIds, cancellationToken);
        var locations = await RefLookup.LoadLocationRefsAsync(context, locationIds, cancellationToken);
        var lots = await RefLookup.LoadLotRefsAsync(context, lotIds, cancellationToken);

        var items = page.Select(i => new InventoryDto(
                i.Id,
                products[i.ProductId],
                locations[i.LocationId],
                i.LotId.HasValue ? lots[i.LotId.Value] : null,
                i.LotId.HasValue ? lots[i.LotId.Value].ExpirationDate : null,
                i.OnHand,
                i.Reserved,
                i.OnHand - i.Reserved))
            .ToList();

        return new PagedResult<InventoryDto>(items, query.Page, query.PageSize, totalCount);
    }
}
