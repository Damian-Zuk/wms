using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Services;

/// <summary>
/// Computes how much capacity, per dimension, a quantity of a product consumes.
/// This is the single extension point for new capacity dimensions: today only
/// <see cref="CapacityDimension.Units"/> is produced (1 physical unit = 1 unit of
/// load). To add weight/volume, read the relevant product attribute here and emit
/// the extra dimension(s); nothing else in the capacity-checking path changes.
/// </summary>
public static class CapacityLoadCalculator
{
    public static IReadOnlyDictionary<CapacityDimension, int> Load(Product product, Quantity quantity)
        => new Dictionary<CapacityDimension, int>
        {
            [CapacityDimension.Units] = quantity.Value
        };
}
