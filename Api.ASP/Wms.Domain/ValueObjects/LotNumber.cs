using Wms.Domain.Primitives;

namespace Wms.Domain.ValueObjects;

public class LotNumber : ValueObject
{
    public string Value { get; }

    public LotNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Lot number cannot be empty");

        Value = value;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
