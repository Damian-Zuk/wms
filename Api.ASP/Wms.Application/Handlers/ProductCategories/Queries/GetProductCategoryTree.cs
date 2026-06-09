using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Common.Messaging;
using Wms.Shared.Common;

namespace Wms.Application.Handlers.ProductCategories.Queries;

public sealed record CategoryTreeNodeDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    int DirectSkuCount,
    int TotalSkuCount,
    IReadOnlyList<CategoryTreeNodeDto> Children);

public sealed record GetProductCategoryTreeQuery() : IQuery<IReadOnlyList<CategoryTreeNodeDto>>;

public sealed class GetProductCategoryTreeQueryHandler(IAppDbContext context)
    : IQueryHandler<GetProductCategoryTreeQuery, IReadOnlyList<CategoryTreeNodeDto>>
{
    public async Task<Result<IReadOnlyList<CategoryTreeNodeDto>>> Handle(
        GetProductCategoryTreeQuery query,
        CancellationToken cancellationToken)
    {
        var categories = await context.ProductCategories
            .AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.ParentId })
            .ToListAsync(cancellationToken);

        var directCounts = await context.Products
            .AsNoTracking()
            .Where(p => p.ProductCategoryId != null)
            .GroupBy(p => p.ProductCategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var directCountById = directCounts
            .Where(x => x.CategoryId.HasValue)
            .ToDictionary(x => x.CategoryId!.Value, x => x.Count);

        var childrenByParent = categories
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).ToList());

        CategoryTreeNodeDto BuildNode(Guid id, string name, Guid? parentId)
        {
            var direct = directCountById.GetValueOrDefault(id, 0);

            var childNodes = childrenByParent.TryGetValue(id, out var kids)
                ? kids.Select(k => BuildNode(k.Id, k.Name, k.ParentId)).ToList()
                : [];

            var total = direct + childNodes.Sum(c => c.TotalSkuCount);

            return new CategoryTreeNodeDto(id, name, parentId, direct, total, childNodes);
        }

        IReadOnlyList<CategoryTreeNodeDto> roots = categories
            .Where(c => c.ParentId is null)
            .OrderBy(c => c.Name)
            .Select(c => BuildNode(c.Id, c.Name, c.ParentId))
            .ToList();

        return Result.Success(roots);
    }
}
