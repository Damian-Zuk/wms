using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;

namespace Wms.Application.Handlers.ProductCategories;

/// <summary>
/// In-memory view over the <c>(Id, ParentId)</c> edges of the category tree.
/// Used to expand a category to its subtree (for rolled-up filters and SKU
/// counts) and to detect cycles when re-parenting a category.
/// </summary>
public sealed class CategoryHierarchy
{
    private readonly Dictionary<Guid, List<Guid>> _childrenByParent = new();

    public CategoryHierarchy(IEnumerable<(Guid Id, Guid? ParentId)> edges)
    {
        foreach (var (id, parentId) in edges)
        {
            if (parentId is not { } parent)
                continue;

            if (!_childrenByParent.TryGetValue(parent, out var children))
                _childrenByParent[parent] = children = [];

            children.Add(id);
        }
    }

    /// <summary>
    /// All category ids in the subtree rooted at <paramref name="rootId"/>,
    /// including the root itself.
    /// </summary>
    public IReadOnlyList<Guid> DescendantIdsInclusive(Guid rootId)
    {
        var result = new List<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(rootId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            result.Add(current);

            if (_childrenByParent.TryGetValue(current, out var children))
                foreach (var child in children)
                    stack.Push(child);
        }

        return result;
    }

    public static async Task<CategoryHierarchy> LoadAsync(
        IAppDbContext context,
        CancellationToken cancellationToken)
    {
        var edges = await context.ProductCategories
            .AsNoTracking()
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync(cancellationToken);

        return new CategoryHierarchy(edges.Select(e => (e.Id, e.ParentId)));
    }
}
