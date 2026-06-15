using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Models;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

/// <summary>
/// One requested receipt line of a <see cref="StockIn"/>: a product (optionally a lot)
/// and the total quantity to receive. The quantity is split across one or more
/// <see cref="StockInItem"/> placements. This entity owns the invariant that the
/// placements always sum to exactly the requested <see cref="Quantity"/>.
/// </summary>
public class StockInLine : Entity
{
    private readonly List<StockInItem> _items = [];
    public IReadOnlyCollection<StockInItem> Items => _items;

    public Guid ProductId { get; private set; }
    public Guid? LotId { get; private set; }

    /// <summary>The total quantity requested for this line.</summary>
    public Quantity Quantity { get; private set; } = new Quantity(0);

    private StockInLine() { }

    public StockInLine(Guid productId, Guid? lotId, Quantity quantity)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        LotId = lotId;
        Quantity = quantity;
    }

    public int PlacedTotal => _items.Sum(i => i.Quantity.Value);

    /// <summary>True once every placement of this line has been fully put away.</summary>
    public bool IsFullyPlaced => _items.All(i => i.IsFullyPlaced);

    /// <summary>
    /// Sets the placements produced by the putaway planner (each carrying the strategy
    /// that picked it). Enforces the sum-equals-requested invariant.
    /// </summary>
    public Result SetPlacements(IReadOnlyList<PlacementAllocation> placements)
    {
        var validation = ValidatePlacements(placements);
        if (validation.IsFailure)
            return validation;

        _items.Clear();
        foreach (var p in placements)
            _items.Add(new StockInItem(p.LocationId, new Quantity(p.Quantity), p.Strategy, p.HandlingUnitId));

        return Result.Success();
    }

    /// <summary>
    /// Replaces the placements with a user-supplied set. Enforces the same invariant
    /// and stamps every placement as <see cref="PutawayStrategyType.Manual"/>.
    /// </summary>
    public Result ReplacePlacementsManual(IEnumerable<(Guid LocationId, int Quantity, Guid? HandlingUnitId)> placements)
    {
        var list = placements
            .Select(p => new PlacementAllocation(p.LocationId, p.Quantity, PutawayStrategyType.Manual, p.HandlingUnitId))
            .ToList();

        var validation = ValidatePlacements(list);
        if (validation.IsFailure)
            return validation;

        _items.Clear();
        foreach (var p in list)
            _items.Add(new StockInItem(p.LocationId, new Quantity(p.Quantity), PutawayStrategyType.Manual, p.HandlingUnitId));

        return Result.Success();
    }

    private Result ValidatePlacements(IReadOnlyList<PlacementAllocation> placements)
    {
        if (placements.Count == 0)
            return StockInErrors.PlacementsRequired();

        if (placements.Any(p => p.Quantity <= 0))
            return StockInErrors.PlacementQuantityMustBePositive();

        var total = placements.Sum(p => p.Quantity);
        if (total != Quantity.Value)
            return StockInErrors.PlacementsDoNotMatchLineTotal(ProductId, Quantity.Value, total);

        // A handling unit is placed whole into one location, so it appears at most once.
        var splitHu = placements
            .Where(p => p.HandlingUnitId.HasValue)
            .GroupBy(p => p.HandlingUnitId!.Value)
            .FirstOrDefault(g => g.Count() > 1);

        if (splitHu is not null)
            return HandlingUnitErrors.HandlingUnitSplitAcrossPlacements(splitHu.Key);

        return Result.Success();
    }
}
