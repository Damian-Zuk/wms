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
        bool isMixedLotAllowed = true,
        decimal? weightCapacity = null,
        decimal? volumeCapacity = null)
    {
        Id = Guid.NewGuid();
        Code = code;
        Address = address;
        Type = type;
        Description = description;
        TemperatureZone = temperatureZone;
        Capacity = new LocationCapacity(capacity, weightCapacity, volumeCapacity);
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
        bool isMixedLotAllowed,
        decimal? weightCapacity = null,
        decimal? volumeCapacity = null)
    {
        Code = code;
        Address = address;
        Type = type;
        Description = description;
        TemperatureZone = temperatureZone;
        Capacity = new LocationCapacity(capacity, weightCapacity, volumeCapacity);
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
        IReadOnlyCollection<Inventory> currentContents,
        IReadOnlyDictionary<Guid, Product> contentsProducts)
        => CanAccept(product, lot, quantity, currentContents, CapacityOccupancy.Empty(), contentsProducts);

    /// <summary>
    /// Validates whether this location can accept <paramref name="quantity"/> of
    /// <paramref name="product"/>. <paramref name="extraOccupancy"/> is space that
    /// must be treated as already used on top of <paramref name="currentContents"/> —
    /// e.g. other stock-ins' active reservations or sibling placements being planned
    /// in the same draft. <paramref name="contentsProducts"/> resolves the products of
    /// <paramref name="currentContents"/> so their weight/volume load can be summed
    /// (the Units dimension needs no lookup). Capacity is checked generically over
    /// every configured dimension and the most restrictive one wins.
    /// </summary>
    public Result CanAccept(
        Product product,
        Lot? lot,
        Quantity quantity,
        IReadOnlyCollection<Inventory> currentContents,
        CapacityOccupancy extraOccupancy,
        IReadOnlyDictionary<Guid, Product> contentsProducts)
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

        var incoming = CapacityLoadCalculator.Load(product, quantity);

        foreach (var dimension in Capacity.ConfiguredDimensions())
        {
            // Capacity = physical occupancy. Reserved units still take up space,
            // so OnHand (not Available) is the right basis here.
            var limit = Capacity.Limit(dimension)!.Value;
            var totalAfter = ExistingLoad(dimension, currentContents, contentsProducts)
                + extraOccupancy.Get(dimension)
                + incoming.GetValueOrDefault(dimension);

            if (totalAfter > limit)
                return LocationErrors.CapacityExceeded(Id, dimension, limit, totalAfter);
        }

        if (!IsMixedSkuAllowed)
        {
            // Another product is physically present if its OnHand > 0,
            // regardless of how much of it is reserved.
            var otherProduct = currentContents.FirstOrDefault(i =>
                i.OnHand.Value > 0 && i.ProductId != product.Id);

            if (otherProduct is not null)
                return LocationErrors.MixedSkuNotAllowed(Id, otherProduct.ProductId);
        }

        if (!IsMixedLotAllowed)
        {
            var otherLot = currentContents.FirstOrDefault(i =>
                i.OnHand.Value > 0 && i.LotId.HasValue && i.LotId != lot?.Id);

            if (otherLot is not null)
                return LocationErrors.MixedLotNotAllowed(Id, otherLot.LotId!.Value);
        }

        return Result.Success();
    }

    /// <summary>
    /// How many additional whole units of <paramref name="product"/> this location can
    /// hold given its current contents plus <paramref name="extraOccupancy"/>, taking
    /// the most restrictive configured dimension (units / weight / volume). Returns null
    /// when no configured dimension constrains the product (caller should place the whole
    /// remainder in one go). Callers MUST first gate with
    /// <see cref="CanAccept(Product, Lot?, Quantity, IReadOnlyCollection{Inventory}, CapacityOccupancy, IReadOnlyDictionary{Guid, Product})"/>
    /// for zone / mixed-SKU / mixed-lot / blocked rules before sizing with this.
    /// </summary>
    public int? UnitsThatFit(
        Product product,
        IReadOnlyCollection<Inventory> currentContents,
        CapacityOccupancy extraOccupancy,
        IReadOnlyDictionary<Guid, Product> contentsProducts)
    {
        var perUnit = CapacityLoadCalculator.Load(product, new Quantity(1));

        int? fit = null;
        foreach (var dimension in Capacity.ConfiguredDimensions())
        {
            var perUnitLoad = perUnit.GetValueOrDefault(dimension);
            if (perUnitLoad <= 0)
                continue; // a zero-load product can never fill this dimension

            var remaining = Capacity.Limit(dimension)!.Value
                - ExistingLoad(dimension, currentContents, contentsProducts)
                - extraOccupancy.Get(dimension);

            var dimensionFit = remaining <= 0 ? 0 : (int)Math.Floor(remaining / perUnitLoad);
            fit = fit is null ? dimensionFit : Math.Min(fit.Value, dimensionFit);
        }

        return fit;
    }

    private static decimal ExistingLoad(
        CapacityDimension dimension,
        IReadOnlyCollection<Inventory> contents,
        IReadOnlyDictionary<Guid, Product> contentsProducts)
    {
        // Units occupancy is one per physical unit regardless of SKU, so it needs no
        // product lookup. Weight/volume occupancy is summed from each line's product.
        if (dimension == CapacityDimension.Units)
            return contents.Sum(i => i.OnHand.Value);

        decimal total = 0;
        foreach (var line in contents)
            if (contentsProducts.TryGetValue(line.ProductId, out var lineProduct))
                total += CapacityLoadCalculator.Load(lineProduct, line.OnHand).GetValueOrDefault(dimension);

        return total;
    }
}
