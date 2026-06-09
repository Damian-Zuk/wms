using Wms.Domain.Primitives;

namespace Wms.Domain.Entities;

/// <summary>
/// A node in the hierarchical product-category tree. A category may have a
/// parent (forming the tree) and products may optionally belong to a category.
/// </summary>
public class ProductCategory : Entity
{
    public string Name { get; private set; } = null!;

    public Guid? ParentId { get; private set; }

    private ProductCategory() { }

    public ProductCategory(string name, Guid? parentId = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        ParentId = parentId;
    }

    public void Rename(string name) => Name = name;

    public void MoveTo(Guid? parentId) => ParentId = parentId;
}
