using FluentAssertions;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Putaway.Strategies;

public class NearestEmptyStrategyTests : IntegrationTestBase
{
    public NearestEmptyStrategyTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Returns_empty_storage_location_with_matching_temperature_zone()
    {
        var product = TestData.Product("NE-1", temperatureZone: TemperatureZone.Ambient);
        var empty = TestData.Location("NE-1-EMPTY", temperatureZone: TemperatureZone.Ambient);

        Context.Products.Add(product);
        Context.Locations.Add(empty);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new NearestEmptyStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(5),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(empty.Id);
        result.StrategyName.Should().Be("NearestEmpty");
    }

    [Fact]
    public async Task Returns_null_when_no_empty_storage_location_exists()
    {
        // Single location, but it has an inventory row (even zero qty makes
        // it "not empty" per the strategy contract).
        var product = TestData.Product("NE-2");
        var loc = TestData.Location("NE-2-LOC");

        Context.Products.Add(product);
        Context.Locations.Add(loc);
        Context.Inventories.Add(TestData.Inventory(product.Id, loc.Id, lotId: null, onHand: 0));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new NearestEmptyStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Temperature_mismatch_excludes_an_otherwise_empty_location()
    {
        // Only candidate is Frozen; product is Ambient → strategy returns null.
        var product = TestData.Product("NE-3", temperatureZone: TemperatureZone.Ambient);
        var wrongZone = TestData.Location("NE-3-LOC", temperatureZone: TemperatureZone.Frozen);

        Context.Products.Add(product);
        Context.Locations.Add(wrongZone);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new NearestEmptyStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Inactive_and_blocked_locations_are_excluded()
    {
        var product = TestData.Product("NE-4");
        var blocked = TestData.Location("NE-4-BLOCKED");
        blocked.Block("audit");
        var inactive = TestData.Location("NE-4-INACTIVE");
        inactive.Deactivate();

        Context.Products.Add(product);
        Context.Locations.AddRange(blocked, inactive);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new NearestEmptyStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Non_storage_locations_are_excluded()
    {
        var product = TestData.Product("NE-5");
        var quarantine = TestData.Location("NE-5-Q", type: LocationType.Quarantine);

        Context.Products.Add(product);
        Context.Locations.Add(quarantine);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new NearestEmptyStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Picks_lexically_first_address_when_multiple_candidates_exist()
    {
        // Addresses are sorted by Zone → Aisle → Rack → Shelf → Bin, lexical
        // per segment. Strategy must pick the smallest address.
        var product = TestData.Product("NE-6");

        // Same Zone/Aisle/Rack/Shelf; bins B01, B02, B10 → string-sorted: B01 < B02 < B10.
        var first = TestData.LocationAt(
            new LocationAddress("Z1", "A1", "R1", "S1", "B01"),
            code: "NE-6-FIRST");
        var middle = TestData.LocationAt(
            new LocationAddress("Z1", "A1", "R1", "S1", "B02"),
            code: "NE-6-MIDDLE");
        var last = TestData.LocationAt(
            new LocationAddress("Z1", "A1", "R1", "S1", "B10"),
            code: "NE-6-LAST");

        Context.Products.Add(product);
        // Add in reverse order to make sure it's the strategy ordering, not
        // insertion order, that wins.
        Context.Locations.AddRange(last, middle, first);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new NearestEmptyStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(first.Id);
    }

    [Fact]
    public async Task Address_ordering_breaks_ties_segment_by_segment()
    {
        // Two candidates differ only in the Aisle segment. Higher-priority
        // segment must dominate even though a later segment looks "smaller".
        var product = TestData.Product("NE-7");

        // a1 vs a2: Aisle decides — "a1" wins even though Bin is "z" vs "a".
        var winsOnAisle = TestData.LocationAt(
            new LocationAddress("Z1", "A1", "R1", "S1", "Z9"),
            code: "NE-7-WIN");
        var losesOnAisle = TestData.LocationAt(
            new LocationAddress("Z1", "A2", "R1", "S1", "A1"),
            code: "NE-7-LOSE");

        Context.Products.Add(product);
        Context.Locations.AddRange(losesOnAisle, winsOnAisle);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new NearestEmptyStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(winsOnAisle.Id);
    }
}
