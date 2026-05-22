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

    public IReadOnlyCollection<ProductPreferredLocation> PreferredLocations => _preferredLocations;

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

    public void Update(string name, string description, TemperatureZone requiredTemperatureZone)
    {
        Name = name;
        Description = description;
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
