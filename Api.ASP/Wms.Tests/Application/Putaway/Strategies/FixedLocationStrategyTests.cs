using FluentAssertions;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Putaway.Strategies;

/// <summary>
/// Strategy is tested in isolation: the IAppDbContext dependency is
/// satisfied by the testcontainer-backed AppDbContext from
/// IntegrationTestBase, but the strategy is exercised directly rather than
/// through PutawayService. PutawayService chain behaviour is covered
/// separately in <see cref="PutawayServiceTests"/>.
/// </summary>
public class FixedLocationStrategyTests : IntegrationTestBase
{
    public FixedLocationStrategyTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Returns_first_preferred_location_that_accepts()
    {
        var product = TestData.Product("FX-1");
        var loc1 = TestData.Location("FX-1-A");
        var loc2 = TestData.Location("FX-1-B");
        product.SetPreferredLocations(new[] { loc1.Id, loc2.Id });

        Context.Locations.AddRange(loc1, loc2);
        Context.Products.Add(product);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new FixedLocationStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(5),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(loc1.Id);
        result.StrategyName.Should().Be("FixedLocation");
    }

    [Fact]
    public async Task Returns_null_when_product_has_no_preferred_locations()
    {
        var product = TestData.Product("FX-2");

        Context.Products.Add(product);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new FixedLocationStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Blocked_preferred_location_returns_null_so_the_chain_falls_through()
    {
        // Single preferred location, blocked. The strategy must not raise an
        // error — returning null is the contract for "no suggestion from me,
        // try the next strategy".
        var product = TestData.Product("FX-3");
        var loc = TestData.Location("FX-3-LOC");
        loc.Block("damaged shelf");
        product.SetPreferredLocations(new[] { loc.Id });

        Context.Locations.Add(loc);
        Context.Products.Add(product);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new FixedLocationStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Full_preferred_location_returns_null_so_the_chain_falls_through()
    {
        // The only preferred location is at capacity. Same null contract.
        var product = TestData.Product("FX-4");
        var loc = TestData.Location("FX-4-LOC", capacity: 10);
        var inv = TestData.Inventory(product.Id, loc.Id, lotId: null, onHand: 10);
        product.SetPreferredLocations(new[] { loc.Id });

        Context.Locations.Add(loc);
        Context.Products.Add(product);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new FixedLocationStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Skips_full_first_choice_and_returns_second_one()
    {
        // First preferred is full; second has room. Verifies the iteration
        // really does fall through within the preferred-list and doesn't
        // give up at the first failure.
        var product = TestData.Product("FX-5");
        var full = TestData.Location("FX-5-FULL", capacity: 5);
        var roomy = TestData.Location("FX-5-ROOMY", capacity: 50);
        Context.Inventories.Add(TestData.Inventory(product.Id, full.Id, null, onHand: 5));
        product.SetPreferredLocations(new[] { full.Id, roomy.Id });

        Context.Locations.AddRange(full, roomy);
        Context.Products.Add(product);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var strategy = new FixedLocationStrategy(Context);

        var result = await strategy.SuggestAsync(
            product, lot: null, new Quantity(3),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.LocationId.Should().Be(roomy.Id);
    }
}
