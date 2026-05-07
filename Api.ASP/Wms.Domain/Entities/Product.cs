using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class Product : Entity
{
    public Sku Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;

    private Product() { }

    public Product(Sku sku, string name, string description = "")
    {
        Id = Guid.NewGuid();
        Name = name;
        Sku = sku;
        Description = description;
    }
}
