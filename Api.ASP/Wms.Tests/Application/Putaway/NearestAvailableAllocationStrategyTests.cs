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
/// Pure unit tests for <see cref="NearestAvailableAllocationStrategy"/>. It targets
/// Storage locations that still have room — finite locations with remaining headroom
/// first, then unlimited-capacity locations — each group ordered by ascending address.
/// Address segments are single characters so ordinal ordering matches numeric ordering.
/// </summary>
public class NearestAvailableAllocationStrategyTests
{
    private static readonly NearestAvailableAllocationStrategy Strategy = new();

    private static Location At(string bin, string code, int? capacity, LocationType type = LocationType.Storage) =>
        TestData.LocationAt(new LocationAddress("Z1", "A1", "R1", "S1", bin), code, type: type, capacity: capacity);

    [Fact]
    public void Ranks_finite_locations_before_unlimited_then_by_ascending_address()
    {
        var product = TestData.Product();
        var finiteB3 = At("B3", "FINITE-B3", capacity: 100);
        var finiteB1 = At("B1", "FINITE-B1", capacity: 100);
        var unlimitedB2 = At("B2", "UNLIMITED-B2", capacity: null);
        var unlimitedB0 = At("B0", "UNLIMITED-B0", capacity: null);
        var context = new PutawayPlanContext([finiteB3, finiteB1, unlimitedB2, unlimitedB0], [], [], [product]);

        var result = Strategy.CandidateLocations(product, null, context);

        // Finite (by address) first, unlimited (by address) last.
        result.Should().Equal(finiteB1.Id, finiteB3.Id, unlimitedB0.Id, unlimitedB2.Id);
    }

    [Fact]
    public void Includes_partially_filled_finite_location_that_still_has_headroom()
    {
        var product = TestData.Product();
        var partial = At("B1", "PARTIAL", capacity: 100);
        var context = new PutawayPlanContext(
            [partial],
            [TestData.Inventory(product.Id, partial.Id, onHand: 90)],
            [],
            [product]);

        var result = Strategy.CandidateLocations(product, null, context);

        result.Should().Equal(partial.Id);
    }

    [Fact]
    public void Excludes_finite_locations_with_no_remaining_capacity()
    {
        var product = TestData.Product();
        var withRoom = At("B1", "ROOM", capacity: 100);
        var fullByStock = At("B2", "FULL-STOCK", capacity: 10);
        var fullByReservation = At("B3", "FULL-RESERVED", capacity: 10);
        var reservation = new CapacityReservation(
            Guid.NewGuid(), Guid.NewGuid(), fullByReservation.Id, product.Id, null, new Quantity(10));
        var context = new PutawayPlanContext(
            [withRoom, fullByStock, fullByReservation],
            [TestData.Inventory(product.Id, fullByStock.Id, onHand: 10)],
            [reservation],
            [product]);

        var result = Strategy.CandidateLocations(product, null, context);

        result.Should().Equal(withRoom.Id);
    }

    [Fact]
    public void Excludes_inactive_blocked_non_storage_and_temperature_mismatched_locations()
    {
        var product = TestData.Product(temperatureZone: TemperatureZone.Ambient);
        var ok = At("B1", "OK", capacity: 100);
        var inactive = At("B2", "INACTIVE", capacity: 100);
        inactive.Deactivate();
        var blocked = At("B3", "BLOCKED", capacity: 100);
        blocked.Block("damaged");
        var quarantine = At("B4", "QUARANTINE", capacity: 100, type: LocationType.Quarantine);
        var cold = TestData.LocationAt(
            new LocationAddress("Z1", "A1", "R1", "S1", "B5"), "COLD",
            temperatureZone: TemperatureZone.Frozen, capacity: null);
        var context = new PutawayPlanContext([ok, inactive, blocked, quarantine, cold], [], [], [product]);

        var result = Strategy.CandidateLocations(product, null, context);

        result.Should().Equal(ok.Id);
    }
}
