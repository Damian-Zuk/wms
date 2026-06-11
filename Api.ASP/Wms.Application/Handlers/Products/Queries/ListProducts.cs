using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Extensions;
using Wms.Application.Handlers.ProductCategories;
using Wms.Domain.Enums;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Products.Queries;

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Weight,
    decimal Volume,
    decimal UnitPrice,
    TemperatureZone RequiredTemperatureZone,
    int OnHand,
    IReadOnlyList<Guid> PreferredLocationIds,
    Guid? CategoryId,
    string? CategoryName);

public sealed record ListProductsQuery(
    string? Search,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20,
    Guid? CategoryId = null,
    TemperatureZone? TemperatureZone = null) : IQuery<PagedResult<ProductDto>>;

public sealed class ListProductsValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

public sealed class ListProductsQueryHandler(IAppDbContext context)
    : IQueryHandler<ListProductsQuery, PagedResult<ProductDto>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(
        ListProductsQuery query,
        CancellationToken cancellationToken)
    {
        var productsQuery = context.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            productsQuery = productsQuery.Where(p =>
                p.Sku.Value.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term));
        }

        if (query.CategoryId.HasValue)
        {
            // Include the whole subtree so filtering a parent surfaces products
            // assigned to any descendant category.
            var hierarchy = await CategoryHierarchy.LoadAsync(context, cancellationToken);
            var subtree = hierarchy.DescendantIdsInclusive(query.CategoryId.Value).ToArray();
            productsQuery = productsQuery.Where(p =>
                p.ProductCategoryId.HasValue && subtree.Contains(p.ProductCategoryId.Value));
        }

        if (query.TemperatureZone.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.RequiredTemperatureZone == query.TemperatureZone.Value);
        }

        var totalCount = await productsQuery.CountAsync(cancellationToken);

        var desc = query.SortDescending;
        productsQuery = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "sku" => productsQuery.OrderByDirection(p => p.Sku.Value, desc),
            "name" => productsQuery.OrderByDirection(p => p.Name, desc),
            "weight" => productsQuery.OrderByDirection(p => p.Weight, desc),
            "volume" => productsQuery.OrderByDirection(p => p.Volume, desc),
            "temperaturezone" => productsQuery.OrderByDirection(p => p.RequiredTemperatureZone, desc),
            "onhand" => productsQuery.OrderByDirection(
                p => context.Inventories.Where(i => i.ProductId == p.Id).Sum(i => i.OnHand.Value),
                desc),
            "unitprice" => productsQuery.OrderByDirection(p => p.UnitPrice, desc),
            "category" => productsQuery.OrderByDirection(
                p => context.ProductCategories
                    .Where(c => c.Id == p.ProductCategoryId)
                    .Select(c => c.Name)
                    .FirstOrDefault(),
                desc),
            _ => productsQuery.OrderBy(p => p.Name),
        };

        var items = await productsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku.Value,
                p.Name,
                p.Description,
                p.Weight,
                p.Volume,
                p.UnitPrice,
                p.RequiredTemperatureZone,
                context.Inventories.Where(i => i.ProductId == p.Id).Sum(i => i.OnHand.Value),
                p.PreferredLocations
                    .OrderBy(pl => pl.Sequence)
                    .Select(pl => pl.LocationId)
                    .ToList(),
                p.ProductCategoryId,
                p.ProductCategoryId == null
                    ? null
                    : context.ProductCategories
                        .Where(c => c.Id == p.ProductCategoryId)
                        .Select(c => c.Name)
                        .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, query.Page, query.PageSize, totalCount);
    }
}
