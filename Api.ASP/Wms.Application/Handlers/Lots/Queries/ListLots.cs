using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Application.Common.Models;
using Wms.Application.Common.Extensions;
using Wms.Application.Handlers.ProductCategories;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.Lots.Queries;

public sealed record ListLotsQuery(
    Guid? ProductId,
    string? Search,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20,
    Guid? CategoryId = null) : IQuery<PagedResult<LotDto>>;

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
            var term = query.Search.Trim().ToLower();
            lotsQuery = lotsQuery.Where(l => l.Number.Value.ToLower().Contains(term));
        }

        if (query.CategoryId.HasValue)
        {
            var hierarchy = await CategoryHierarchy.LoadAsync(context, cancellationToken);
            var subtree = hierarchy.DescendantIdsInclusive(query.CategoryId.Value).ToArray();
            lotsQuery = lotsQuery.Where(l => context.Products.Any(p =>
                p.Id == l.ProductId &&
                p.ProductCategoryId.HasValue &&
                subtree.Contains(p.ProductCategoryId.Value)));
        }

        var totalCount = await lotsQuery.CountAsync(cancellationToken);

        var desc = query.SortDescending;
        lotsQuery = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "number" => lotsQuery.OrderByDirection(l => l.Number.Value, desc),
            "product" => lotsQuery.OrderByDirection(
                l => context.Products
                    .Where(p => p.Id == l.ProductId)
                    .Select(p => p.Sku.Value)
                    .FirstOrDefault(),
                desc),
            "category" => lotsQuery.OrderByDirection(
                l => context.Products
                    .Where(p => p.Id == l.ProductId)
                    .Select(p => context.ProductCategories
                        .Where(c => c.Id == p.ProductCategoryId)
                        .Select(c => c.Name)
                        .FirstOrDefault())
                    .FirstOrDefault(),
                desc),
            "onhand" => lotsQuery.OrderByDirection(
                l => context.Inventories.Where(i => i.LotId == l.Id).Sum(i => i.OnHand.Value),
                desc),
            "manufacturedate" => lotsQuery.OrderByDirection(l => l.ManufactureDate, desc),
            "expirationdate" => lotsQuery.OrderByDirection(l => l.ExpirationDate, desc),
            _ => lotsQuery.OrderBy(l => l.Number.Value),
        };

        var items = await lotsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(l => new LotDto(
                l.Id,
                l.Number.Value,
                l.ProductId,
                l.ManufactureDate,
                l.ExpirationDate,
                l.ExpirationDate != null && l.ExpirationDate.Value < today,
                l.ExpirationDate != null && l.ExpirationDate.Value <= soonThreshold,
                context.Inventories.Where(i => i.LotId == l.Id).Sum(i => i.OnHand.Value)))
            .ToListAsync(cancellationToken);

        return new PagedResult<LotDto>(items, query.Page, query.PageSize, totalCount);
    }
}
