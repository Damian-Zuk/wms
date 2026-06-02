using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Domain.Services;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class Location : Entity
{
    public LocationCode Code { get; private set; } = null!;
    public LocationAddress Address { get; private set; } = null!;
    public string? Description { get; private set; }
    public LocationType Type { get; private set; }
    public TemperatureZone TemperatureZone { get; private set; } = TemperatureZone.Ambient;
    public LocationCapacity Capacity { get; private set; } = LocationCapacity.Unlimited;
    public bool IsMixedSkuAllowed { get; private set; } = true;
    public bool IsMixedLotAllowed { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public bool IsBlocked { get; private set; }
    public string? BlockedReason { get; private set; }

    private Location() { }

    public Location(
        LocationCode code,
        LocationAddress address,
        LocationType type,
        string? description = null,
        TemperatureZone temperatureZone = TemperatureZone.Ambient,
        int? capacity = null,
        bool isMixedSkuAllowed = true,
        bool isMixedLotAllowed = true)
    {
        Id = Guid.NewGuid();
        Code = code;
        Address = address;
        Type = type;
        Description = description;
        TemperatureZone = temperatureZone;
        Capacity = new LocationCapacity(capacity);
        IsMixedSkuAllowed = isMixedSkuAllowed;
        IsMixedLotAllowed = isMixedLotAllowed;
        IsActive = true;
        IsBlocked = false;
    }

    public void Update(
        LocationCode code,
        LocationAddress address,
        LocationType type,
        string? description,
        TemperatureZone temperatureZone,
        int? capacity,
        bool isMixedSkuAllowed,
        bool isMixedLotAllowed)
    {
        Code = code;
        Address = address;
        Type = type;
        Description = description;
        TemperatureZone = temperatureZone;
        Capacity = new LocationCapacity(capacity);
        IsMixedSkuAllowed = isMixedSkuAllowed;
        IsMixedLotAllowed = isMixedLotAllowed;
    }

    public Result Block(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return LocationErrors.LocationBlocked(Id, null);

        IsBlocked = true;
        BlockedReason = reason;
        return Result.Success();
    }

    public Result Unblock()
    {
        IsBlocked = false;
        BlockedReason = null;
        return Result.Success();
    }

    public Result Activate()
    {
        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        IsActive = false;
        return Result.Success();
    }

    public Result CanAccept(
        Product product,
        Lot? lot,
        Quantity quantity,
        IEnumerable<Inventory> currentContents)
        => CanAccept(product, lot, quantity, currentContents, CapacityOccupancy.Empty());

    /// <summary>
    /// Validates whether this location can accept <paramref name="quantity"/> of
    /// <paramref name="product"/>. <paramref name="extraOccupancy"/> is space that
    /// must be treated as already used on top of <paramref name="currentContents"/> —
    /// e.g. other stock-ins' active reservations or sibling placements being planned
    /// in the same draft. Capacity is checked generically over every configured
    /// dimension so new dimensions (weight, volume, …) need no change here.
    /// </summary>
    public Result CanAccept(
        Product product,
        Lot? lot,
        Quantity quantity,
        IEnumerable<Inventory> currentContents,
        CapacityOccupancy extraOccupancy)
    {
        if (IsBlocked)
            return LocationErrors.LocationBlocked(Id, BlockedReason);

        if (!IsActive)
            return LocationErrors.LocationInactive(Id);

        if (TemperatureZone != product.RequiredTemperatureZone)
            return LocationErrors.TemperatureMismatch(
                Id,
                TemperatureZone,
                product.RequiredTemperatureZone);

        var contents = currentContents as IReadOnlyCollection<Inventory> ?? currentContents.ToList();
        var incoming = CapacityLoadCalculator.Load(product, quantity);

        foreach (var dimension in Capacity.ConfiguredDimensions())
        {
            // Capacity = physical occupancy. Reserved units still take up space,
            // so OnHand (not Available) is the right basis here.
            var limit = Capacity.Limit(dimension)!.Value;
            var totalAfter = ExistingLoad(dimension, contents)
                + extraOccupancy.Get(dimension)
                + incoming.GetValueOrDefault(dimension);

            if (totalAfter > limit)
                return LocationErrors.CapacityExceeded(Id, dimension, limit, totalAfter);
        }

        if (!IsMixedSkuAllowed)
        {
            // Another product is physically present if its OnHand > 0,
            // regardless of how much of it is reserved.
            var otherProduct = contents.FirstOrDefault(i =>
                i.OnHand.Value > 0 && i.ProductId != product.Id);

            if (otherProduct is not null)
                return LocationErrors.MixedSkuNotAllowed(Id, otherProduct.ProductId);
        }

        if (!IsMixedLotAllowed)
        {
            var otherLot = contents.FirstOrDefault(i =>
                i.OnHand.Value > 0 && i.LotId.HasValue && i.LotId != lot?.Id);

            if (otherLot is not null)
                return LocationErrors.MixedLotNotAllowed(Id, otherLot.LotId!.Value);
        }

        return Result.Success();
    }

    /// <summary>
    /// How many additional units this location can physically hold given its current
    /// contents plus <paramref name="extraOccupancy"/>. Returns null when the Units
    /// dimension is unlimited (caller should place the whole remainder in one go).
    /// This only sizes against the Units capacity — callers MUST first gate with
    /// <see cref="CanAccept(Product, Lot?, Quantity, IEnumerable{Inventory}, CapacityOccupancy)"/>
    /// for zone / mixed-SKU / mixed-lot / blocked rules before sizing with this.
    /// </summary>
    public int? UnitsThatFit(IEnumerable<Inventory> currentContents, CapacityOccupancy extraOccupancy)
    {
        if (!Capacity.MaxUnits.HasValue)
            return null;

        var used = currentContents.Sum(i => i.OnHand.Value) + extraOccupancy.Get(CapacityDimension.Units);
        return Math.Max(0, Capacity.MaxUnits.Value - used);
    }

    private static int ExistingLoad(CapacityDimension dimension, IReadOnlyCollection<Inventory> contents) => dimension switch
    {
        // Physical occupancy is per unit regardless of which SKU occupies the space.
        CapacityDimension.Units => contents.Sum(i => i.OnHand.Value),
        _ => 0
    };
}
