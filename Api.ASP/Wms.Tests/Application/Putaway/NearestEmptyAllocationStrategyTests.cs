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
/// Pure unit tests for <see cref="NearestEmptyAllocationStrategy"/>. It targets only
/// finite-capacity Storage locations that are completely empty (no on-hand inventory
/// and no reserved capacity), ordered by ascending address. Address segments are
/// single characters so ordinal ordering matches the obvious numeric ordering.
/// </summary>
public class NearestEmptyAllocationStrategyTests
{
    private static readonly NearestEmptyAllocationStrategy Strategy = new();

    private static Location At(string bin, string code, int? capacity, LocationType type = LocationType.Storage) =>
        TestData.LocationAt(new LocationAddress("Z1", "A1", "R1", "S1", bin), code, type: type, capacity: capacity);

    [Fact]
    public void Ranks_empty_finite_locations_by_ascending_address()
    {
        var product = TestData.Product();
        var third = At("B3", "THIRD", capacity: 100);
        var first = At("B1", "FIRST", capacity: 100);
        var second = At("B2", "SECOND", capacity: 100);
        var context = new PutawayPlanContext([third, first, second], [], []);

        var result = Strategy.CandidateLocations(product, null, context);

        result.Should().Equal(first.Id, second.Id, third.Id);
    }

    [Fact]
    public void Excludes_unlimited_capacity_locations()
    {
        var product = TestData.Product();
        var finite = At("B1", "FINITE", capacity: 100);
        var unlimited = At("B2", "UNLIMITED", capacity: null);
        var context = new PutawayPlanContext([finite, unlimited], [], []);

        var result = Strategy.CandidateLocations(product, null, context);

        result.Should().Equal(finite.Id);
    }

    [Fact]
    public void Excludes_locations_that_hold_on_hand_inventory()
    {
        var product = TestData.Product();
        var empty = At("B1", "EMPTY", capacity: 100);
        var occupied = At("B2", "OCCUPIED", capacity: 100);
        var context = new PutawayPlanContext(
            [empty, occupied],
            [TestData.Inventory(product.Id, occupied.Id, onHand: 1)],
            []);

        var result = Strategy.CandidateLocations(product, null, context);

        result.Should().Equal(empty.Id);
    }

    [Fact]
    public void Excludes_locations_whose_capacity_is_reserved_by_another_stock_in()
    {
        var product = TestData.Product();
        var empty = At("B1", "EMPTY", capacity: 100);
        var reserved = At("B2", "RESERVED", capacity: 100);
        var reservation = new CapacityReservation(
            Guid.NewGuid(), Guid.NewGuid(), reserved.Id, product.Id, null, new Quantity(10));
        var context = new PutawayPlanContext([empty, reserved], [], [reservation]);

        var result = Strategy.CandidateLocations(product, null, context);

        // No on-hand inventory, but the reservation means it is not empty.
        result.Should().Equal(empty.Id);
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
            temperatureZone: TemperatureZone.Frozen, capacity: 100);
        var context = new PutawayPlanContext([ok, inactive, blocked, quarantine, cold], [], []);

        var result = Strategy.CandidateLocations(product, null, context);

        result.Should().Equal(ok.Id);
    }
}
