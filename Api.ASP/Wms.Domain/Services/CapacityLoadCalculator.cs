using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Services;

/// <summary>
/// Computes how much capacity, per dimension, a quantity of a product consumes:
/// one unit of load per physical unit, the product's weight (kg) per unit, and the
/// product's volume (dm³) per unit. This is the single place that maps product
/// attributes to capacity load; every dimension is always emitted and the capacity
/// check simply ignores the ones a location does not constrain.
/// </summary>
public static class CapacityLoadCalculator
{
    public static IReadOnlyDictionary<CapacityDimension, decimal> Load(Product product, Quantity quantity)
        => new Dictionary<CapacityDimension, decimal>
        {
            [CapacityDimension.Units] = quantity.Value,
            [CapacityDimension.Weight] = product.Weight * quantity.Value,
            [CapacityDimension.Volume] = product.Volume * quantity.Value
        };
}
