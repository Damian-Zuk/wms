using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;

namespace Wms.Tests.Common;

/// <summary>
/// Small object-mother helpers so test arrange-blocks stay focused on what
/// matters to the assertion (e.g. expiry dates for FEFO) instead of
/// boilerplate like LocationAddress segments.
/// </summary>
public static class TestData
{
    private static int _addressCounter;

    public static Product Product(
        string sku = "SKU-1",
        string name = "Widget",
        TemperatureZone temperatureZone = TemperatureZone.Ambient) =>
        new(new Sku(sku), name, "test product", temperatureZone);

    public static Location Location(
        string code = "LOC-1",
        LocationType type = LocationType.Storage,
        TemperatureZone temperatureZone = TemperatureZone.Ambient,
        int? capacity = null,
        bool isMixedSkuAllowed = true,
        bool isMixedLotAllowed = true) =>
        new(
            new LocationCode(code),
            UniqueAddress(),
            type,
            description: null,
            temperatureZone: temperatureZone,
            capacity: capacity,
            isMixedSkuAllowed: isMixedSkuAllowed,
            isMixedLotAllowed: isMixedLotAllowed);

    public static Lot Lot(
        Guid productId,
        string number = "LOT-1",
        DateOnly? expirationDate = null) =>
        new(new LotNumber(number), productId, manufactureDate: null, expirationDate: expirationDate);

    public static Inventory Inventory(
        Guid productId,
        Guid locationId,
        Guid? lotId = null,
        int onHand = 0)
    {
        var inv = new Inventory(productId, locationId, lotId);
        if (onHand > 0)
        {
            inv.Increase(new Quantity(onHand));
        }
        return inv;
    }

    /// <summary>
    /// Each location needs a unique address (DB has a unique index on the
    /// five address segments). Use a counter so tests can ignore that
    /// detail entirely.
    /// </summary>
    public static LocationAddress UniqueAddress()
    {
        var n = Interlocked.Increment(ref _addressCounter);
        return new LocationAddress("Z1", "A1", "R1", "S1", $"B{n}");
    }
}
