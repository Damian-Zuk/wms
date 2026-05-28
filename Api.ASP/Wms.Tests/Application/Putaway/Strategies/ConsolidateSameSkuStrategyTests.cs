using FluentAssertions;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Putaway.Strategies;

public class ConsolidateSameSkuStrategyTests : IntegrationTestBase
{
    public ConsolidateSameSkuStrategyTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Returns_a_location_that_already_holds_the_same_product()
    {
        var product = TestData.Product("CS-1");
        var loc = TestData.Location("CS-1-LOC", capacity: 100);
        var inv = TestData.Inventory(product.Id, loc.Id, lotId: null, onHand: 3);

        Context.Products.Add(product);
        Context.Locations.Add(loc);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new ConsolidateSameSkuStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(5),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(loc.Id);
        result.StrategyName.Should().Be("ConsolidateSameSku");
    }

    [Fact]
    public async Task Returns_null_when_no_location_holds_the_product()
    {
        var product = TestData.Product("CS-2");

        Context.Products.Add(product);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new ConsolidateSameSkuStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Picks_finite_capacity_location_over_unlimited_one()
    {
        // Both hold the product. Finite-capacity location should win because
        // strategy deprioritises unlimited locations.
        var product = TestData.Product("CS-3");
        var finite = TestData.Location("CS-3-FIN", capacity: 50);
        var unlimited = TestData.Location("CS-3-INF", capacity: null);

        Context.Products.Add(product);
        Context.Locations.AddRange(finite, unlimited);
        Context.Inventories.AddRange(
            TestData.Inventory(product.Id, finite.Id, null, onHand: 10),
            TestData.Inventory(product.Id, unlimited.Id, null, onHand: 10));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new ConsolidateSameSkuStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(finite.Id);
    }

    [Fact]
    public async Task Among_finite_locations_picks_the_one_with_least_remaining_capacity()
    {
        // Two finite-capacity candidates with different remaining headroom.
        // Strategy is "fill-up-first": location with LEAST remaining capacity
        // wins so partially-full locations get topped up before fresh ones.
        var product = TestData.Product("CS-4");
        var nearlyFull = TestData.Location("CS-4-FULL", capacity: 100);  // 90 used → 10 remaining
        var spacious = TestData.Location("CS-4-SPACE", capacity: 100);   // 10 used → 90 remaining

        Context.Products.Add(product);
        Context.Locations.AddRange(nearlyFull, spacious);
        Context.Inventories.AddRange(
            TestData.Inventory(product.Id, nearlyFull.Id, null, onHand: 90),
            TestData.Inventory(product.Id, spacious.Id, null, onHand: 10));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new ConsolidateSameSkuStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(5),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(nearlyFull.Id);
    }

    [Fact]
    public async Task Skips_a_candidate_that_fails_can_accept()
    {
        // Both hold the product, but the "best" candidate (least remaining)
        // is over capacity for the requested putaway. Should be skipped.
        var product = TestData.Product("CS-5");
        var tight = TestData.Location("CS-5-TIGHT", capacity: 10);   // 9 used → 1 remaining, can't fit 5
        var loose = TestData.Location("CS-5-LOOSE", capacity: 100);  // 1 used → 99 remaining

        Context.Products.Add(product);
        Context.Locations.AddRange(tight, loose);
        Context.Inventories.AddRange(
            TestData.Inventory(product.Id, tight.Id, null, onHand: 9),
            TestData.Inventory(product.Id, loose.Id, null, onHand: 1));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new ConsolidateSameSkuStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(5),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(loose.Id);
    }

    public class LotMatching : ConsolidateSameSkuStrategyTests
    {
        public LotMatching(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Null_incoming_lot_only_matches_null_lot_rows()
        {
            // Same product in two locations:
            //   locWithLot: stored under a specific Lot
            //   locWithoutLot: stored without a lot
            // Caller's lot is null → only locWithoutLot is a candidate.
            var product = TestData.Product("CS-LM-1");
            var lot = TestData.Lot(product.Id, "LOT-A");
            var locWithLot = TestData.Location("CS-LM-1-LOT", capacity: 100);
            var locWithoutLot = TestData.Location("CS-LM-1-NOLOT", capacity: 100);

            Context.Products.Add(product);
            Context.Lots.Add(lot);
            Context.Locations.AddRange(locWithLot, locWithoutLot);
            Context.Inventories.AddRange(
                TestData.Inventory(product.Id, locWithLot.Id, lot.Id, onHand: 5),
                TestData.Inventory(product.Id, locWithoutLot.Id, lotId: null, onHand: 5));
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var strategy = new ConsolidateSameSkuStrategy(Context);

            var result = await strategy.SuggestAsync(
                product, lot: null, new Quantity(1),
                TestContext.Current.CancellationToken);

            result.Should().NotBeNull();
            result!.LocationId.Should().Be(locWithoutLot.Id);
        }

        [Fact]
        public async Task Non_null_incoming_lot_only_matches_same_lot_rows()
        {
            // Same product in two locations, each holding a different lot.
            // Caller specifies lotA → only locA is a candidate.
            var product = TestData.Product("CS-LM-2");
            var lotA = TestData.Lot(product.Id, "LOT-A");
            var lotB = TestData.Lot(product.Id, "LOT-B");
            var locA = TestData.Location("CS-LM-2-A", capacity: 100);
            var locB = TestData.Location("CS-LM-2-B", capacity: 100);

            Context.Products.Add(product);
            Context.Lots.AddRange(lotA, lotB);
            Context.Locations.AddRange(locA, locB);
            Context.Inventories.AddRange(
                TestData.Inventory(product.Id, locA.Id, lotA.Id, onHand: 5),
                TestData.Inventory(product.Id, locB.Id, lotB.Id, onHand: 5));
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var strategy = new ConsolidateSameSkuStrategy(Context);

            var result = await strategy.SuggestAsync(
                product, lot: lotA, new Quantity(1),
                TestContext.Current.CancellationToken);

            result.Should().NotBeNull();
            result!.LocationId.Should().Be(locA.Id);
        }

        [Fact]
        public async Task Returns_null_when_only_a_different_lot_is_present()
        {
            var product = TestData.Product("CS-LM-3");
            var presentLot = TestData.Lot(product.Id, "LOT-PRESENT");
            var requestedLot = TestData.Lot(product.Id, "LOT-REQUESTED");
            var loc = TestData.Location("CS-LM-3-LOC", capacity: 100);

            Context.Products.Add(product);
            Context.Lots.AddRange(presentLot, requestedLot);
            Context.Locations.Add(loc);
            Context.Inventories.Add(
                TestData.Inventory(product.Id, loc.Id, presentLot.Id, onHand: 5));
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var strategy = new ConsolidateSameSkuStrategy(Context);

            var result = await strategy.SuggestAsync(
                product, lot: requestedLot, new Quantity(1),
                TestContext.Current.CancellationToken);

            result.Should().BeNull();
        }
    }
}
