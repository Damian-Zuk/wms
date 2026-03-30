using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class Location : Entity
{
    public LocationCode Code { get; private set; } = null!;
    public string? Description { get; private set; }

    private Location() { }

    public Location(LocationCode code, string? description = null)
    {
        Id = Guid.NewGuid();
        Code = code;
        Description = description;
    }
}
