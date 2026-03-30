using Wms.Domain.Primitives;

namespace Wms.Domain.ValueObjects;

public class LocationCode : ValueObject
{
    public string Value { get; }

    public LocationCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Location code cannot be empty");

        Value = value;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}

