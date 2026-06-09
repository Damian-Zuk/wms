using Wms.Domain.Enums;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class Product : Entity
{
    private readonly List<ProductPreferredLocation> _preferredLocations = [];

    public Sku Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public TemperatureZone RequiredTemperatureZone { get; private set; } = TemperatureZone.Ambient;

    public Guid? ProductCategoryId { get; private set; }

    /// <summary>Weight of one unit in kilograms.</summary>
    public decimal Weight { get; private set; }

    /// <summary>Volume of one unit in cubic decimetres.</summary>
    public decimal Volume { get; private set; }

    public IReadOnlyCollection<ProductPreferredLocation> PreferredLocations => _preferredLocations;

    private Product() { }

    public Product(
        Sku sku,
        string name,
        decimal weight,
        decimal volume,
        string description = "",
        TemperatureZone requiredTemperatureZone = TemperatureZone.Ambient,
        Guid? categoryId = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        Sku = sku;
        Weight = weight;
        Volume = volume;
        Description = description;
        RequiredTemperatureZone = requiredTemperatureZone;
        ProductCategoryId = categoryId;
    }

    public void SetCategory(Guid? categoryId) => ProductCategoryId = categoryId;

    public void Update(
        string name,
        string description,
        decimal weight,
        decimal volume,
        TemperatureZone requiredTemperatureZone)
    {
        Name = name;
        Description = description;
        Weight = weight;
        Volume = volume;
        RequiredTemperatureZone = requiredTemperatureZone;
    }

    public void SetPreferredLocations(IEnumerable<Guid> locationIds)
    {
        _preferredLocations.Clear();

        var seen = new HashSet<Guid>();
        var sequence = 0;

        foreach (var locationId in locationIds)
        {
            if (locationId == Guid.Empty)
                continue;

            if (!seen.Add(locationId))
                continue;

            _preferredLocations.Add(new ProductPreferredLocation(Id, locationId, sequence));
            sequence++;
        }
    }
}
