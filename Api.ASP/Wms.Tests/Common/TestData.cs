using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Models;
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
        TemperatureZone temperatureZone = TemperatureZone.Ambient,
        decimal weight = 1m,
        decimal volume = 1m,
        decimal unitPrice = 1m) =>
        new(new Sku(sku), name, weight, volume, unitPrice, "test product", temperatureZone);

    public static Location Location(
        string code = "LOC-1",
        LocationType type = LocationType.Storage,
        TemperatureZone temperatureZone = TemperatureZone.Ambient,
        int? capacity = null,
        bool isMixedSkuAllowed = true,
        bool isMixedLotAllowed = true,
        decimal? weightCapacity = null,
        decimal? volumeCapacity = null) =>
        new(
            new LocationCode(code),
            UniqueAddress(),
            type,
            description: null,
            temperatureZone: temperatureZone,
            capacity: capacity,
            isMixedSkuAllowed: isMixedSkuAllowed,
            isMixedLotAllowed: isMixedLotAllowed,
            weightCapacity: weightCapacity,
            volumeCapacity: volumeCapacity);

    public static Location LocationAt(
        LocationAddress address,
        string code = "LOC-1",
        LocationType type = LocationType.Storage,
        TemperatureZone temperatureZone = TemperatureZone.Ambient,
        int? capacity = null,
        bool isMixedSkuAllowed = true,
        bool isMixedLotAllowed = true,
        decimal? weightCapacity = null,
        decimal? volumeCapacity = null) =>
        new(
            new LocationCode(code),
            address,
            type,
            description: null,
            temperatureZone: temperatureZone,
            capacity: capacity,
            isMixedSkuAllowed: isMixedSkuAllowed,
            isMixedLotAllowed: isMixedLotAllowed,
            weightCapacity: weightCapacity,
            volumeCapacity: volumeCapacity);

    public static Lot Lot(
        Guid productId,
        string number = "LOT-1",
        DateOnly? expirationDate = null) =>
        new(new LotNumber(number), productId, manufactureDate: null, expirationDate: expirationDate);

    public static Inventory Inventory(
        Guid productId,
        Guid locationId,
        Guid? lotId = null,
        int onHand = 0,
        DateTime? receivedAt = null)
    {
        var inv = new Inventory(productId, locationId, lotId);
        if (onHand > 0)
        {
            if (receivedAt.HasValue)
                inv.Receive(new Quantity(onHand), receivedAt.Value);
            else
                inv.Increase(new Quantity(onHand));
        }
        return inv;
    }

    /// <summary>
    /// A StockIn with a single line placed entirely into one location. Keeps the
    /// arrange-block short for tests that only care about a one-line, one-placement
    /// receipt.
    /// </summary>
    public static StockIn StockIn(
        Guid productId,
        Guid locationId,
        int quantity,
        Guid? lotId = null,
        PutawayStrategyType strategy = PutawayStrategyType.NearestEmpty)
    {
        var stockIn = new StockIn(Guid.NewGuid());
        stockIn.AddLineWithPlacements(
            productId,
            lotId,
            new Quantity(quantity),
            [new(locationId, quantity, strategy)]);
        return stockIn;
    }

    /// <summary>
    /// A StockOut with a single line whose quantity is drawn entirely from one
    /// location (and lot). Keeps the arrange-block short for tests that only care
    /// about a one-line, one-allocation pick.
    /// </summary>
    public static StockOut StockOut(
        Guid productId,
        Guid locationId,
        int quantity,
        Guid? lotId = null,
        PickingStrategyType strategy = PickingStrategyType.Fefo)
    {
        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddLineWithAllocations(
            productId,
            strategy,
            new Quantity(quantity),
            [new PickAllocation(locationId, lotId, quantity, strategy)]);
        return stockOut;
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
