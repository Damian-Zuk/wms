using System.Diagnostics.CodeAnalysis;
using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Shared.Common;

namespace Wms.Domain.ValueObjects;

public class LocationAddress : ValueObject
{
    public const int MaxSegmentLength = 8;
    public const char Separator = '-';
    private const int SegmentCount = 5;

    public string Zone { get; }
    public string Aisle { get; }
    public string Rack { get; }
    public string Shelf { get; }
    public string Bin { get; }

    public LocationAddress(string zone, string aisle, string rack, string shelf, string bin)
    {
        EnsureValidSegment(zone, nameof(zone));
        EnsureValidSegment(aisle, nameof(aisle));
        EnsureValidSegment(rack, nameof(rack));
        EnsureValidSegment(shelf, nameof(shelf));
        EnsureValidSegment(bin, nameof(bin));

        Zone = zone;
        Aisle = aisle;
        Rack = rack;
        Shelf = shelf;
        Bin = bin;
    }

    public static Result<LocationAddress> Create(
        string zone,
        string aisle,
        string rack,
        string shelf,
        string bin)
    {
        if (!TryValidateSegment(zone, nameof(zone), out var error) ||
            !TryValidateSegment(aisle, nameof(aisle), out error) ||
            !TryValidateSegment(rack, nameof(rack), out error) ||
            !TryValidateSegment(shelf, nameof(shelf), out error) ||
            !TryValidateSegment(bin, nameof(bin), out error))
        {
            return Result.Failure<LocationAddress>(error!);
        }

        return new LocationAddress(zone, aisle, rack, shelf, bin);
    }

    public static LocationAddress Parse(string value)
    {
        if (TryParse(value, out var address))
            return address;

        throw new FormatException($"Invalid location address format: '{value}'.");
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out LocationAddress? address)
    {
        address = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(Separator);
        if (parts.Length != SegmentCount)
            return false;

        if (parts.Any(p => !IsValidSegment(p)))
            return false;

        address = new LocationAddress(parts[0], parts[1], parts[2], parts[3], parts[4]);
        return true;
    }

    public override string ToString() => string.Join(Separator, Zone, Aisle, Rack, Shelf, Bin);

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Zone;
        yield return Aisle;
        yield return Rack;
        yield return Shelf;
        yield return Bin;
    }

    private static void EnsureValidSegment(string segment, string name)
    {
        if (!IsValidSegment(segment))
            throw new ArgumentException(
                $"Address segment '{name}' must be non-empty, alphanumeric, and at most {MaxSegmentLength} characters.",
                name);
    }

    private static bool TryValidateSegment(string segment, string name, out Error? error)
    {
        if (!IsValidSegment(segment))
        {
            error = LocationErrors.InvalidLocationAddress(
                $"segment '{name}' must be non-empty, alphanumeric, and at most {MaxSegmentLength} characters.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsValidSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        if (segment.Length > MaxSegmentLength)
            return false;

        foreach (var ch in segment)
        {
            if (!char.IsLetterOrDigit(ch))
                return false;
        }

        return true;
    }
}
