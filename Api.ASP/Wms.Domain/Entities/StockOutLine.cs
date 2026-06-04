using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Models;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

/// <summary>
/// One requested line of a <see cref="StockOut"/>: a product, the picking strategy
/// to use, and the total quantity to pick. The quantity is split across one or more
/// <see cref="StockOutItem"/> picks produced by the planner. This entity owns the
/// invariant that the items always sum to exactly the requested <see cref="Quantity"/>.
/// </summary>
public class StockOutLine : Entity
{
    private readonly List<StockOutItem> _items = [];
    public IReadOnlyCollection<StockOutItem> Items => _items;

    public Guid ProductId { get; private set; }
    public PickingStrategyType Strategy { get; private set; }

    /// <summary>The total quantity requested for this line.</summary>
    public Quantity Quantity { get; private set; } = new Quantity(0);

    private StockOutLine() { }

    public StockOutLine(Guid productId, PickingStrategyType strategy, Quantity quantity)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Strategy = strategy;
        Quantity = quantity;
    }

    public int PickedTotal => _items.Sum(i => i.PickedQuantity.Value);

    /// <summary>True once every item of this line has been fully picked.</summary>
    public bool IsFullyPicked => _items.All(i => i.IsFullyPicked);

    /// <summary>
    /// Sets the allocations produced by the picking planner (each carrying the
    /// location, lot and strategy that picked it). Enforces the sum-equals-requested
    /// invariant.
    /// </summary>
    public Result SetAllocations(IReadOnlyList<PickAllocation> allocations)
    {
        var validation = ValidateAllocations(allocations);
        if (validation.IsFailure)
            return validation;

        _items.Clear();
        foreach (var a in allocations)
            _items.Add(new StockOutItem(a.LocationId, a.LotId, new Quantity(a.Quantity), a.Strategy));

        return Result.Success();
    }

    private Result ValidateAllocations(IReadOnlyList<PickAllocation> allocations)
    {
        if (allocations.Count == 0)
            return StockOutErrors.AllocationsRequired();

        if (allocations.Any(a => a.Quantity <= 0))
            return StockOutErrors.AllocationQuantityMustBePositive();

        var total = allocations.Sum(a => a.Quantity);
        if (total != Quantity.Value)
            return StockOutErrors.AllocationsDoNotMatchLineTotal(ProductId, Quantity.Value, total);

        return Result.Success();
    }
}
