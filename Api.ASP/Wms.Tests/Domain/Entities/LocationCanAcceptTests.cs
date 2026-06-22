using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Services;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class LocationCanAcceptTests
{
    private static Location BuildLocation(
        TemperatureZone zone = TemperatureZone.Ambient,
        int? capacity = null,
        bool mixedSku = true,
        bool mixedLot = true,
        decimal? weightCapacity = null,
        decimal? volumeCapacity = null) =>
        new(
            new LocationCode($"LOC-{Guid.NewGuid():N}"[..10]),
            TestData.UniqueAddress(),
            LocationType.Storage,
            description: null,
            temperatureZone: zone,
            capacity: capacity,
            isMixedSkuAllowed: mixedSku,
            isMixedLotAllowed: mixedLot,
            weightCapacity: weightCapacity,
            volumeCapacity: volumeCapacity);

    private static Product BuildProduct(
        TemperatureZone zone = TemperatureZone.Ambient,
        decimal weight = 1m,
        decimal volume = 1m,
        decimal unitPrice = 1m) =>
        new(new Sku($"SKU-{Guid.NewGuid():N}"[..10]), "p", weight, volume, unitPrice, "", zone);

    private static Inventory BuildInventory(Guid productId, Guid locationId, Guid? lotId, int onHand)
    {
        var inv = new Inventory(productId, locationId, lotId);
        if (onHand > 0)
            inv.Increase(new Quantity(onHand));
        return inv;
    }

    private static IReadOnlyDictionary<Guid, Product> Lookup(params Product[] products) =>
        products.ToDictionary(p => p.Id);

    // Pending (not-yet-on-hand) capacity held at the bin: another stock-in's reservation
    // or a sibling placement in the same draft, carrying its product/lot identity.
    private static CapacityOccupancy PendingOccupancy(Product product, Lot? lot, int units = 1)
    {
        var occupancy = new CapacityOccupancy();
        occupancy.Add(CapacityLoadCalculator.Load(product, new Quantity(units)), product.Id, lot?.Id);
        return occupancy;
    }

    [Fact]
    public void Empty_location_accepts_compatible_product()
    {
        var loc = BuildLocation();
        var product = BuildProduct();

        var result = loc.CanAccept(product, lot: null, new Quantity(5), Array.Empty<Inventory>(), Lookup(product));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Blocked_location_is_rejected()
    {
        var loc = BuildLocation();
        loc.Block("damaged shelf");
        var product = BuildProduct();

        var result = loc.CanAccept(product, lot: null, new Quantity(1), Array.Empty<Inventory>(), Lookup(product));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.Blocked");
    }

    [Fact]
    public void Inactive_location_is_rejected()
    {
        var loc = BuildLocation();
        loc.Deactivate();
        var product = BuildProduct();

        var result = loc.CanAccept(product, lot: null, new Quantity(1), Array.Empty<Inventory>(), Lookup(product));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.Inactive");
    }

    public class TemperatureZoneRules
    {
        [Fact]
        public void Mismatch_is_rejected()
        {
            var loc = BuildLocation(zone: TemperatureZone.Frozen);
            var product = BuildProduct(zone: TemperatureZone.Ambient);

            var result = loc.CanAccept(product, lot: null, new Quantity(1), Array.Empty<Inventory>(), Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.TemperatureMismatch");
        }

        [Fact]
        public void Matching_zone_is_accepted()
        {
            var loc = BuildLocation(zone: TemperatureZone.Chilled);
            var product = BuildProduct(zone: TemperatureZone.Chilled);

            var result = loc.CanAccept(product, lot: null, new Quantity(1), Array.Empty<Inventory>(), Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }
    }

    public class CapacityRules
    {
        [Fact]
        public void Below_capacity_is_accepted()
        {
            var loc = BuildLocation(capacity: 10);
            var product = BuildProduct();
            var existing = BuildInventory(product.Id, loc.Id, null, onHand: 4);

            var result = loc.CanAccept(product, null, new Quantity(5), new[] { existing }, Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Exactly_at_capacity_is_accepted()
        {
            var loc = BuildLocation(capacity: 10);
            var product = BuildProduct();
            var existing = BuildInventory(product.Id, loc.Id, null, onHand: 4);

            var result = loc.CanAccept(product, null, new Quantity(6), new[] { existing }, Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Over_capacity_is_rejected()
        {
            var loc = BuildLocation(capacity: 10);
            var product = BuildProduct();
            var existing = BuildInventory(product.Id, loc.Id, null, onHand: 7);

            var result = loc.CanAccept(product, null, new Quantity(5), new[] { existing }, Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.CapacityExceeded");
        }

        [Fact]
        public void Capacity_check_uses_on_hand_not_available()
        {
            // Reserved units still occupy space; capacity is a physical check.
            var loc = BuildLocation(capacity: 10);
            var product = BuildProduct();
            var existing = BuildInventory(product.Id, loc.Id, null, onHand: 8);
            existing.Reserve(new Quantity(6));

            var result = loc.CanAccept(product, null, new Quantity(5), new[] { existing }, Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.CapacityExceeded");
        }

        [Fact]
        public void Unlimited_capacity_is_always_accepted()
        {
            var loc = BuildLocation(capacity: null);
            var product = BuildProduct();
            var existing = BuildInventory(product.Id, loc.Id, null, onHand: 1_000_000);

            var result = loc.CanAccept(product, null, new Quantity(1_000_000), new[] { existing }, Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }
    }

    public class WeightAndVolumeRules
    {
        [Fact]
        public void Over_weight_capacity_is_rejected()
        {
            // 4 units on hand = 8 kg; +2 units (4 kg) → 12 kg > 10 kg.
            var loc = BuildLocation(weightCapacity: 10m);
            var product = BuildProduct(weight: 2m);
            var existing = BuildInventory(product.Id, loc.Id, null, onHand: 4);

            var result = loc.CanAccept(product, null, new Quantity(2), new[] { existing }, Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.CapacityExceeded");
        }

        [Fact]
        public void Over_volume_capacity_is_rejected()
        {
            var loc = BuildLocation(volumeCapacity: 5m);
            var product = BuildProduct(volume: 1m);

            var result = loc.CanAccept(product, null, new Quantity(6), Array.Empty<Inventory>(), Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.CapacityExceeded");
        }

        [Fact]
        public void Most_restrictive_dimension_wins()
        {
            // Units fit (limit 100) but weight does not (limit 10 kg, 2 kg/unit → 6 units = 12 kg).
            var loc = BuildLocation(capacity: 100, weightCapacity: 10m);
            var product = BuildProduct(weight: 2m);

            var result = loc.CanAccept(product, null, new Quantity(6), Array.Empty<Inventory>(), Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.CapacityExceeded");
        }

        [Fact]
        public void Existing_contents_weight_counts_against_a_different_incoming_sku()
        {
            // A heavy SKU already on hand fills most of the weight; a light SKU still won't fit.
            var loc = BuildLocation(weightCapacity: 10m);
            var heavy = BuildProduct(weight: 3m);
            var light = BuildProduct(weight: 1m);
            var existing = BuildInventory(heavy.Id, loc.Id, null, onHand: 3); // 9 kg

            // 9 kg existing + 2 kg incoming = 11 kg > 10 kg.
            var result = loc.CanAccept(light, null, new Quantity(2), new[] { existing }, Lookup(heavy, light));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.CapacityExceeded");
        }

        [Fact]
        public void Within_every_dimension_is_accepted()
        {
            var loc = BuildLocation(capacity: 100, weightCapacity: 100m, volumeCapacity: 100m);
            var product = BuildProduct(weight: 2m, volume: 1m);

            var result = loc.CanAccept(product, null, new Quantity(10), Array.Empty<Inventory>(), Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }
    }

    public class MixedSkuRules
    {
        [Fact]
        public void Single_sku_location_rejects_a_second_product()
        {
            var loc = BuildLocation(mixedSku: false);
            var product = BuildProduct();
            var otherProduct = BuildProduct();
            var existing = BuildInventory(otherProduct.Id, loc.Id, null, onHand: 1);

            var result = loc.CanAccept(product, null, new Quantity(1), new[] { existing }, Lookup(product, otherProduct));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.MixedSkuNotAllowed");
        }

        [Fact]
        public void Single_sku_location_accepts_more_of_same_product()
        {
            var loc = BuildLocation(mixedSku: false);
            var product = BuildProduct();
            var existing = BuildInventory(product.Id, loc.Id, null, onHand: 2);

            var result = loc.CanAccept(product, null, new Quantity(3), new[] { existing }, Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Single_sku_location_ignores_a_row_that_was_fully_drained()
        {
            // An inventory row with OnHand == 0 represents prior history; the
            // location is physically empty so a different SKU can move in.
            var loc = BuildLocation(mixedSku: false);
            var product = BuildProduct();
            var otherProduct = BuildProduct();
            var drained = BuildInventory(otherProduct.Id, loc.Id, null, onHand: 0);

            var result = loc.CanAccept(product, null, new Quantity(5), new[] { drained }, Lookup(product, otherProduct));

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Single_sku_location_rejects_a_pending_reservation_for_another_product()
        {
            // No committed inventory — only a pending capacity hold (another stock-in's
            // reservation, or a sibling placement) for a different SKU. Still rejected.
            var loc = BuildLocation(mixedSku: false);
            var product = BuildProduct();
            var otherProduct = BuildProduct();
            var pending = PendingOccupancy(otherProduct, lot: null);

            var result = loc.CanAccept(
                product, null, new Quantity(1), Array.Empty<Inventory>(), pending, Lookup(product, otherProduct));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.MixedSkuNotAllowed");
        }

        [Fact]
        public void Single_sku_location_accepts_a_pending_reservation_for_the_same_product()
        {
            var loc = BuildLocation(mixedSku: false);
            var product = BuildProduct();
            var pending = PendingOccupancy(product, lot: null);

            var result = loc.CanAccept(
                product, null, new Quantity(1), Array.Empty<Inventory>(), pending, Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }
    }

    public class MixedLotRules
    {
        [Fact]
        public void Single_lot_location_rejects_a_second_lot_for_same_product()
        {
            var loc = BuildLocation(mixedLot: false);
            var product = BuildProduct();
            var existingLotId = Guid.NewGuid();
            var existing = BuildInventory(product.Id, loc.Id, existingLotId, onHand: 1);

            var newLot = new Lot(new LotNumber("NEW"), product.Id);

            var result = loc.CanAccept(product, newLot, new Quantity(1), new[] { existing }, Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.MixedLotNotAllowed");
        }

        [Fact]
        public void Single_lot_location_accepts_more_of_the_same_lot()
        {
            var loc = BuildLocation(mixedLot: false);
            var product = BuildProduct();
            var lot = new Lot(new LotNumber("LOT-A"), product.Id);
            var existing = BuildInventory(product.Id, loc.Id, lot.Id, onHand: 1);

            var result = loc.CanAccept(product, lot, new Quantity(1), new[] { existing }, Lookup(product));

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Single_lot_location_rejects_a_pending_reservation_for_another_lot()
        {
            // No committed inventory — only a pending hold for a different lot of the
            // same product (another stock-in or a sibling placement). Still rejected.
            var loc = BuildLocation(mixedLot: false);
            var product = BuildProduct();
            var newLot = new Lot(new LotNumber("NEW"), product.Id);
            var pendingLot = new Lot(new LotNumber("OLD"), product.Id);
            var pending = PendingOccupancy(product, pendingLot);

            var result = loc.CanAccept(
                product, newLot, new Quantity(1), Array.Empty<Inventory>(), pending, Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.MixedLotNotAllowed");
        }

        [Fact]
        public void Single_lot_location_rejects_a_lotless_item_when_a_lot_is_pending()
        {
            // A lotless incoming item conflicts with any pending lot, mirroring the
            // committed-inventory rule which flags any row whose LotId.HasValue.
            var loc = BuildLocation(mixedLot: false);
            var product = BuildProduct();
            var pendingLot = new Lot(new LotNumber("LOT-A"), product.Id);
            var pending = PendingOccupancy(product, pendingLot);

            var result = loc.CanAccept(
                product, lot: null, new Quantity(1), Array.Empty<Inventory>(), pending, Lookup(product));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.MixedLotNotAllowed");
        }
    }

    [Fact]
    public void Combination_of_failures_returns_the_first_one_in_check_order()
    {
        // Blocked + temperature + capacity all fail; the implementation
        // documents Blocked as the earliest check.
        var loc = BuildLocation(zone: TemperatureZone.Frozen, capacity: 1);
        loc.Block("audit");
        var product = BuildProduct(zone: TemperatureZone.Ambient);

        var result = loc.CanAccept(product, null, new Quantity(100), Array.Empty<Inventory>(), Lookup(product));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.Blocked");
    }
}
