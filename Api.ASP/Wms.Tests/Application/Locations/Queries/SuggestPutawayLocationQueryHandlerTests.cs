using FluentAssertions;
using Wms.Application.Common.Data;
using Wms.Application.Features.Locations.Queries;
using Wms.Application.Putaway;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Locations.Queries;

/// <summary>
/// End-to-end integration test for the SuggestPutawayLocation query:
/// real database, real strategies wired in the production order
/// (Fixed → ConsolidateSameSku → NearestEmpty), real PutawayService.
/// </summary>
public class SuggestPutawayLocationQueryHandlerTests : IntegrationTestBase
{
    public SuggestPutawayLocationQueryHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Fires_fixed_location_when_product_has_a_preferred_location_that_accepts()
    {
        var product = TestData.Product("EP-1");
        var preferred = TestData.Location("EP-1-PREF");
        var other = TestData.Location("EP-1-OTHER");
        // Add an existing inventory row for the product elsewhere so
        // Consolidate WOULD return a result if Fixed had not won — proves
        // the chain stopped at Fixed.
        Context.Inventories.Add(TestData.Inventory(product.Id, other.Id, null, onHand: 1));
        product.SetPreferredLocations(new[] { preferred.Id });

        Context.Products.Add(product);
        Context.Locations.AddRange(preferred, other);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = BuildHandler(Context);

        var result = await handler.Handle(
            new SuggestPutawayLocationQuery(product.Id, LotId: null, Quantity: 3),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.StrategyName.Should().Be("FixedLocation");
        result.Value.LocationId.Should().Be(preferred.Id);
        result.Value.LocationCode.Should().Be("EP-1-PREF");
        result.Value.LocationAddress.Should().Contain("-");
    }

    [Fact]
    public async Task Falls_through_to_consolidate_when_no_preferred_location_is_set()
    {
        var product = TestData.Product("EP-2");
        var existing = TestData.Location("EP-2-EXIST", capacity: 100);
        Context.Inventories.Add(TestData.Inventory(product.Id, existing.Id, null, onHand: 5));

        Context.Products.Add(product);
        Context.Locations.Add(existing);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = BuildHandler(Context);

        var result = await handler.Handle(
            new SuggestPutawayLocationQuery(product.Id, null, 3),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.StrategyName.Should().Be("ConsolidateSameSku");
        result.Value.LocationId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Falls_through_to_nearest_empty_when_no_existing_inventory_for_the_product()
    {
        var product = TestData.Product("EP-3");
        var emptyLoc = TestData.Location("EP-3-EMPTY");

        Context.Products.Add(product);
        Context.Locations.Add(emptyLoc);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = BuildHandler(Context);

        var result = await handler.Handle(
            new SuggestPutawayLocationQuery(product.Id, null, 1),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.StrategyName.Should().Be("NearestEmpty");
        result.Value.LocationId.Should().Be(emptyLoc.Id);
    }

    [Fact]
    public async Task Returns_no_suitable_location_when_no_strategy_can_place_the_putaway()
    {
        // Product exists but no preferred location, no existing inventory,
        // no compatible empty Storage location either — only a Quarantine
        // location and a Frozen one (product is Ambient).
        var product = TestData.Product("EP-4", temperatureZone: Wms.Domain.Enums.TemperatureZone.Ambient);
        var quarantine = TestData.Location("EP-4-Q", type: Wms.Domain.Enums.LocationType.Quarantine);
        var frozen = TestData.Location("EP-4-F", temperatureZone: Wms.Domain.Enums.TemperatureZone.Frozen);

        Context.Products.Add(product);
        Context.Locations.AddRange(quarantine, frozen);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = BuildHandler(Context);

        var result = await handler.Handle(
            new SuggestPutawayLocationQuery(product.Id, null, 1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Putaway.NoSuitableLocation");
    }

    [Fact]
    public async Task Lot_tracked_putaway_is_routed_through_the_lot_branch_of_consolidate()
    {
        // Same product in two rows: one without a lot, one with the
        // requested lot. With LotId set, ConsolidateSameSku must pick the
        // row that matches the lot (not the lotless row).
        var product = TestData.Product("EP-5");
        var lot = TestData.Lot(product.Id, "LOT-A");
        var lotlessLoc = TestData.Location("EP-5-LOTLESS", capacity: 100);
        var lotMatchLoc = TestData.Location("EP-5-LOT", capacity: 100);

        Context.Products.Add(product);
        Context.Lots.Add(lot);
        Context.Locations.AddRange(lotlessLoc, lotMatchLoc);
        Context.Inventories.AddRange(
            TestData.Inventory(product.Id, lotlessLoc.Id, lotId: null, onHand: 5),
            TestData.Inventory(product.Id, lotMatchLoc.Id, lotId: lot.Id, onHand: 5));
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = BuildHandler(Context);

        var result = await handler.Handle(
            new SuggestPutawayLocationQuery(product.Id, LotId: lot.Id, Quantity: 1),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.StrategyName.Should().Be("ConsolidateSameSku");
        result.Value.LocationId.Should().Be(lotMatchLoc.Id);
    }

    /// <summary>
    /// Wires the production strategy order (Fixed → ConsolidateSameSku →
    /// NearestEmpty) around a real PutawayService and the query handler.
    /// </summary>
    private static SuggestPutawayLocationQueryHandler BuildHandler(IAppDbContext context)
    {
        var strategies = new IPutawayStrategy[]
        {
            new FixedLocationStrategy(context),
            new ConsolidateSameSkuStrategy(context),
            new NearestEmptyStrategy(context),
        };
        var service = new PutawayService(context, strategies);
        return new SuggestPutawayLocationQueryHandler(service, context);
    }
}
