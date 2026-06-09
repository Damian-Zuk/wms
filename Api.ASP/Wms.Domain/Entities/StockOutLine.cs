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

    /// <summary>
    /// Replaces the allocations with a user-supplied set (location + optional lot +
    /// quantity). Enforces the same sum-equals-requested invariant and stamps both the
    /// line and every item as <see cref="PickingStrategyType.Manual"/>.
    /// </summary>
    public Result ReplaceAllocationsManual(IEnumerable<(Guid LocationId, Guid? LotId, int Quantity)> allocations)
    {
        var list = allocations
            .Select(a => new PickAllocation(a.LocationId, a.LotId, a.Quantity, PickingStrategyType.Manual))
            .ToList();

        var validation = ValidateAllocations(list);
        if (validation.IsFailure)
            return validation;

        Strategy = PickingStrategyType.Manual;
        _items.Clear();
        foreach (var a in list)
            _items.Add(new StockOutItem(a.LocationId, a.LotId, new Quantity(a.Quantity), PickingStrategyType.Manual));

        return Result.Success();
    }

    /// <summary>
    /// Replaces the allocations with a freshly planned set (each item keeping the
    /// strategy stamped on its allocation) and records the <paramref name="strategy"/>
    /// the planner used on the line. Enforces the same sum-equals-requested invariant.
    /// </summary>
    public Result ReplacePlannedAllocations(
        PickingStrategyType strategy,
        IReadOnlyList<PickAllocation> allocations)
    {
        var validation = ValidateAllocations(allocations);
        if (validation.IsFailure)
            return validation;

        Strategy = strategy;
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
