using Wms.Domain.Primitives;

namespace Wms.Domain.ValueObjects;

public class Quantity : ValueObject
{
    public int Value { get; }

    public Quantity(int value)
    {
        if (value < 0)
            throw new ArgumentException("Quantity cannot be negative");

        Value = value;
    }

    public Quantity Add(Quantity other)
    {
        // Throw on int overflow instead of silently wrapping into negatives
        // (which the Quantity invariant then rejects with an unrelated error).
        checked
        {
            return new(Value + other.Value);
        }
    }

    public Quantity Subtract(Quantity other)
    {
        if (Value < other.Value)
            throw new InvalidOperationException("Insufficient quantity");

        return new(Value - other.Value);
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
