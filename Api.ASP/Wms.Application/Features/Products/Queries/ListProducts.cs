using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Application.Common.Models;
using Wms.Application.Extensions;
using Wms.Shared.Common;

namespace Wms.Application.Features.Products.Queries;

public record ProductDto(Guid Id, string Sku, string Name, string Description);

public sealed record ListProductsQuery(
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ProductDto>>;

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
        var productsQuery = context.Products
            .AsNoTracking().AsQueryable().ApplyIsDeletedFilter();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            productsQuery = productsQuery.Where(p =>
                p.Sku.Value.Contains(term) ||
                p.Name.Contains(term));
        }

        var totalCount = await productsQuery.CountAsync(cancellationToken);

        var items = await productsQuery
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(p.Id, p.Sku.Value, p.Name, p.Description))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, query.Page, query.PageSize, totalCount);
    }
}
