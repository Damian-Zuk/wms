using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.ProductCategories.Queries;

public sealed record ProductCategoryDto(Guid Id, string Name, Guid? ParentId);

public sealed record ListProductCategoriesQuery() : IQuery<IReadOnlyList<ProductCategoryDto>>;

public sealed class ListProductCategoriesQueryHandler(IAppDbContext context)
    : IQueryHandler<ListProductCategoriesQuery, IReadOnlyList<ProductCategoryDto>>
{
    public async Task<Result<IReadOnlyList<ProductCategoryDto>>> Handle(
        ListProductCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductCategoryDto> categories = await context.ProductCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryDto(c.Id, c.Name, c.ParentId))
            .ToListAsync(cancellationToken);

        return Result.Success(categories);
    }
}
