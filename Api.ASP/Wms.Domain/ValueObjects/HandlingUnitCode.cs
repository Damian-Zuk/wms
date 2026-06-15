using Wms.Domain.Primitives;

namespace Wms.Domain.ValueObjects;

public class HandlingUnitCode : ValueObject
{
    public string Value { get; }

    public HandlingUnitCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Handling unit code cannot be empty");

        Value = value;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
