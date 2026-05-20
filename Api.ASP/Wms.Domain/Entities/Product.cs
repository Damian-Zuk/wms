using Wms.Domain.Enums;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class Product : Entity
{
    public Sku Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public TemperatureZone RequiredTemperatureZone { get; set; } = TemperatureZone.Ambient;

    private Product() { }

    public Product(
        Sku sku,
        string name,
        string description = "",
        TemperatureZone requiredTemperatureZone = TemperatureZone.Ambient)
    {
        Id = Guid.NewGuid();
        Name = name;
        Sku = sku;
        Description = description;
        RequiredTemperatureZone = requiredTemperatureZone;
    }
}
