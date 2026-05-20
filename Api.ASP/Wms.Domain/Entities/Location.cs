using Wms.Domain.Enums;
using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class Location : Entity
{
    public LocationCode Code { get; set; } = null!;
    public LocationAddress Address { get; set; } = null!;
    public string? Description { get; set; }
    public LocationType Type { get; set; }
    public TemperatureZone TemperatureZone { get; set; } = TemperatureZone.Ambient;
    public int? Capacity { get; set; }
    public bool IsMixedSkuAllowed { get; set; } = true;
    public bool IsMixedLotAllowed { get; set; } = true;
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
        Capacity = capacity;
        IsMixedSkuAllowed = isMixedSkuAllowed;
        IsMixedLotAllowed = isMixedLotAllowed;
        IsActive = true;
        IsBlocked = false;
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

        if (Capacity.HasValue)
        {
            var totalAfter = contents.Sum(i => i.Quantity.Value) + quantity.Value;
            if (totalAfter > Capacity.Value)
                return LocationErrors.CapacityExceeded(Id, Capacity.Value, totalAfter);
        }

        if (!IsMixedSkuAllowed)
        {
            var otherProduct = contents.FirstOrDefault(i =>
                i.Quantity.Value > 0 && i.ProductId != product.Id);

            if (otherProduct is not null)
                return LocationErrors.MixedSkuNotAllowed(Id, otherProduct.ProductId);
        }

        if (!IsMixedLotAllowed)
        {
            var otherLot = contents.FirstOrDefault(i =>
                i.Quantity.Value > 0 && i.LotId.HasValue && i.LotId != lot?.Id);

            if (otherLot is not null)
                return LocationErrors.MixedLotNotAllowed(Id, otherLot.LotId!.Value);
        }

        return Result.Success();
    }
}
