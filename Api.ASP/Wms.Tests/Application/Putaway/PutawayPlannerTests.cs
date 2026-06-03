using FluentAssertions;
using Wms.Application.Putaway;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Putaway;

/// <summary>
/// Pure unit tests for the multi-location planner over an in-memory snapshot
/// (no database). Addresses are set explicitly so NearestEmpty ordering is
/// deterministic.
/// </summary>
public class PutawayPlannerTests
{
    private static PutawayPlanner NewPlanner() => new(
    [
        new PreferredLocationAllocationStrategy(),
        new ConsolidateSameSkuAllocationStrategy(),
        new NearestEmptyAllocationStrategy()
    ]);

    private static Location StorageAt(string bin, string code, int? capacity) =>
        TestData.LocationAt(new LocationAddress("Z1", "A1", "R1", "S1", bin), code, capacity: capacity);

    [Fact]
    public void Splits_across_multiple_empty_locations_filling_each_to_capacity()
    {
        var product = TestData.Product();
        var locA = StorageAt("B1", "A", capacity: 100);
        var locB = StorageAt("B2", "B", capacity: 100);
        var context = new PutawayPlanContext([locA, locB], [], []);

        var result = NewPlanner().Plan(product, null, new Quantity(150), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sum(a => a.Quantity).Should().Be(150);
        result.Value.Should().HaveCount(2);
        result.Value.Single(a => a.LocationId == locA.Id).Quantity.Should().Be(100);
        result.Value.Single(a => a.LocationId == locB.Id).Quantity.Should().Be(50);
        result.Value.Should().OnlyContain(a => a.Strategy == PutawayStrategyType.NearestEmpty);
    }

    [Fact]
    public void Fails_when_total_capacity_is_insufficient()
    {
        var product = TestData.Product();
        var loc = StorageAt("B1", "ONLY", capacity: 100);
        var context = new PutawayPlanContext([loc], [], []);

        var result = NewPlanner().Plan(product, null, new Quantity(150), context);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Putaway.CannotPlaceFullQuantity");
    }

    [Fact]
    public void Unlimited_location_absorbs_the_whole_remainder_in_one_placement()
    {
        var product = TestData.Product();
        var loc = StorageAt("B1", "INF", capacity: null);
        var context = new PutawayPlanContext([loc], [], []);

        var result = NewPlanner().Plan(product, null, new Quantity(500), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Quantity.Should().Be(500);
    }

    [Fact]
    public void Consolidates_into_existing_stock_before_using_empty_locations()
    {
        var product = TestData.Product();
        // 'existing' holds the product with 10 headroom; its address sorts AFTER the
        // empty one, proving consolidation wins on strategy order, not address.
        var existing = StorageAt("B9", "EXIST", capacity: 100);
        var empty = StorageAt("B1", "EMPTY", capacity: 100);
        var inventory = TestData.Inventory(product.Id, existing.Id, onHand: 90);
        var context = new PutawayPlanContext([existing, empty], [inventory], []);

        var result = NewPlanner().Plan(product, null, new Quantity(15), context);

        result.IsSuccess.Should().BeTrue();
        var toExisting = result.Value.Single(a => a.LocationId == existing.Id);
        toExisting.Quantity.Should().Be(10);
        toExisting.Strategy.Should().Be(PutawayStrategyType.ConsolidateSameSku);
        var toEmpty = result.Value.Single(a => a.LocationId == empty.Id);
        toEmpty.Quantity.Should().Be(5);
        toEmpty.Strategy.Should().Be(PutawayStrategyType.NearestEmpty);
    }

    [Fact]
    public void Routes_around_a_location_already_reserved_by_another_stock_in()
    {
        var product = TestData.Product();
        var reserved = StorageAt("B1", "RES", capacity: 100);
        var free = StorageAt("B2", "FREE", capacity: 100);
        var otherReservation = new CapacityReservation(
            Guid.NewGuid(), Guid.NewGuid(), reserved.Id, product.Id, null, new Quantity(100));
        var context = new PutawayPlanContext([reserved, free], [], [otherReservation]);

        var result = NewPlanner().Plan(product, null, new Quantity(50), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.LocationId.Should().Be(free.Id);
    }

    [Fact]
    public void Accumulates_occupancy_across_lines_sharing_the_context()
    {
        var product = TestData.Product();
        var loc = StorageAt("B1", "SHARED", capacity: 100);
        var context = new PutawayPlanContext([loc], [], []);
        var planner = NewPlanner();

        var first = planner.Plan(product, null, new Quantity(70), context);
        first.IsSuccess.Should().BeTrue();
        first.Value.Single().Quantity.Should().Be(70);

        // Only 30 headroom remains; the second line of 40 can't be placed.
        var second = planner.Plan(product, null, new Quantity(40), context);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Putaway.CannotPlaceFullQuantity");
    }

    [Fact]
    public void Skips_mixed_sku_disabled_location_that_holds_another_product()
    {
        var product = TestData.Product("WANTED");
        var otherProduct = TestData.Product("OTHER");

        // Make the mixed-sku-disabled location the product's PREFERRED location so
        // PreferredLocation proposes it first; the planner's CanAccept gate must reject
        // it (a foreign product is present) and fall through to the empty one.
        var preferred = StorageAt("B1", "NO-MIX", capacity: 100);
        SetMixedSkuDisallowed(preferred);
        product.SetPreferredLocations([preferred.Id]);

        var free = StorageAt("B2", "FREE", capacity: 100);
        var foreignStock = TestData.Inventory(otherProduct.Id, preferred.Id, onHand: 10);
        var context = new PutawayPlanContext([preferred, free], [foreignStock], []);

        var result = NewPlanner().Plan(product, null, new Quantity(20), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.LocationId.Should().Be(free.Id);
    }

    [Fact]
    public void Does_not_mix_two_skus_into_a_mixed_sku_disabled_bin_across_lines_of_one_draft()
    {
        var productA = TestData.Product("A");
        var productB = TestData.Product("B");

        // A single mixed-SKU-disabled bin, empty, big enough for either product alone.
        var bin = StorageAt("B1", "NO-MIX", capacity: 100);
        SetMixedSkuDisallowed(bin);
        var context = new PutawayPlanContext([bin], [], []);
        var planner = NewPlanner();

        var first = planner.Plan(productA, null, new Quantity(20), context);
        first.IsSuccess.Should().BeTrue();
        first.Value.Single().LocationId.Should().Be(bin.Id);

        // The bin holds no inventory yet, but A is already planned into it; B must not
        // be mixed in. With no other location available, B's line cannot be placed.
        var second = planner.Plan(productB, null, new Quantity(20), context);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Putaway.CannotPlaceFullQuantity");
    }

    [Fact]
    public void Routes_second_sku_to_a_free_bin_instead_of_mixing_within_one_draft()
    {
        var productA = TestData.Product("A");
        var productB = TestData.Product("B");

        var noMix = StorageAt("B1", "NO-MIX", capacity: 100);
        SetMixedSkuDisallowed(noMix);
        var free = StorageAt("B2", "FREE", capacity: 100);
        var context = new PutawayPlanContext([noMix, free], [], []);
        var planner = NewPlanner();

        var first = planner.Plan(productA, null, new Quantity(20), context);
        first.IsSuccess.Should().BeTrue();
        first.Value.Single().LocationId.Should().Be(noMix.Id);

        var second = planner.Plan(productB, null, new Quantity(20), context);
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().ContainSingle().Which.LocationId.Should().Be(free.Id);
    }

    [Fact]
    public void Does_not_mix_two_lots_into_a_mixed_lot_disabled_bin_across_lines_of_one_draft()
    {
        var product = TestData.Product("LOTTED");
        var lot1 = TestData.Lot(product.Id, "LOT-1", new DateOnly(2027, 1, 1));
        var lot2 = TestData.Lot(product.Id, "LOT-2", new DateOnly(2027, 2, 1));

        var bin = StorageAt("B1", "NO-LOT-MIX", capacity: 100);
        SetMixedLotDisallowed(bin);
        var context = new PutawayPlanContext([bin], [], []);
        var planner = NewPlanner();

        var first = planner.Plan(product, lot1, new Quantity(20), context);
        first.IsSuccess.Should().BeTrue();
        first.Value.Single().LocationId.Should().Be(bin.Id);

        var second = planner.Plan(product, lot2, new Quantity(20), context);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Putaway.CannotPlaceFullQuantity");
    }

    private static void SetMixedSkuDisallowed(Location location) =>
        location.Update(
            location.Code,
            location.Address,
            location.Type,
            location.Description,
            location.TemperatureZone,
            location.Capacity.MaxUnits,
            isMixedSkuAllowed: false,
            isMixedLotAllowed: true);

    private static void SetMixedLotDisallowed(Location location) =>
        location.Update(
            location.Code,
            location.Address,
            location.Type,
            location.Description,
            location.TemperatureZone,
            location.Capacity.MaxUnits,
            isMixedSkuAllowed: true,
            isMixedLotAllowed: false);
}
