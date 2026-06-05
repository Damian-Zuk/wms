using FluentAssertions;
using Wms.Application.Putaway;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Putaway;

/// <summary>
/// Pure unit tests for <see cref="ProximityAllocationStrategy"/>'s candidate
/// ordering over an in-memory snapshot. Address segments are single characters so
/// ordinal string ordering matches the obvious numeric ordering.
/// </summary>
public class ProximityAllocationStrategyTests
{
    private static readonly ProximityAllocationStrategy Strategy = new();

    private static Location At(string zone, string aisle, string rack, string shelf, string bin, string code) =>
        TestData.LocationAt(new LocationAddress(zone, aisle, rack, shelf, bin), code);

    [Fact]
    public void Radiates_from_the_anchor_bin_first_ascending_then_descending_then_widening()
    {
        var product = TestData.Product();
        var anchor = At("Z1", "A1", "R1", "S1", "B2", "ANCHOR");

        var binUpNear = At("Z1", "A1", "R1", "S1", "B3", "BIN-UP-NEAR");
        var binUpFar = At("Z1", "A1", "R1", "S1", "B5", "BIN-UP-FAR");
        var binDown = At("Z1", "A1", "R1", "S1", "B1", "BIN-DOWN");
        var shelfUp = At("Z1", "A1", "R1", "S3", "B1", "SHELF-UP");
        var shelfDown = At("Z1", "A1", "R1", "S0", "B9", "SHELF-DOWN");
        var rackUp = At("Z1", "A1", "R3", "S1", "B1", "RACK-UP");
        var aisleUp = At("Z1", "A3", "R1", "S1", "B1", "AISLE-UP");
        var zoneUp = At("Z3", "A1", "R1", "S1", "B1", "ZONE-UP");

        var locations = new[]
        {
            zoneUp, aisleUp, rackUp, shelfDown, shelfUp, binDown, binUpFar, binUpNear, anchor,
        };
        var context = new PutawayPlanContext(
            locations,
            [TestData.Inventory(product.Id, anchor.Id, onHand: 5)],
            [],
            [product]);

        var result = Strategy.CandidateLocations(product, null, context);

        // Bin ascending (nearest first), then bin descending, then the same
        // ascending-before-descending pattern widening Shelf → Rack → Aisle → Zone.
        // The anchor itself is never a candidate.
        result.Should().Equal(
            binUpNear.Id,
            binUpFar.Id,
            binDown.Id,
            shelfUp.Id,
            shelfDown.Id,
            rackUp.Id,
            aisleUp.Id,
            zoneUp.Id);
    }

    [Fact]
    public void Returns_nothing_when_no_location_holds_the_product()
    {
        var product = TestData.Product();
        var a = At("Z1", "A1", "R1", "S1", "B1", "A");
        var b = At("Z1", "A1", "R1", "S1", "B2", "B");
        var context = new PutawayPlanContext([a, b], [], [], [product]);

        Strategy.CandidateLocations(product, null, context).Should().BeEmpty();
    }

    [Fact]
    public void Anchors_on_the_same_lot_when_one_holds_it()
    {
        var product = TestData.Product();
        var lot1 = TestData.Lot(product.Id, "LOT-1");
        var lot2 = TestData.Lot(product.Id, "LOT-2");

        var lotAnchor = At("Z1", "A1", "R1", "S1", "B5", "LOT-ANCHOR");
        var nearLot = At("Z1", "A1", "R1", "S1", "B6", "NEAR-LOT");
        var skuAnchor = At("Z2", "A1", "R1", "S1", "B5", "SKU-ANCHOR");
        var nearSku = At("Z2", "A1", "R1", "S1", "B6", "NEAR-SKU");

        var context = new PutawayPlanContext(
            [lotAnchor, nearLot, skuAnchor, nearSku],
            [
                TestData.Inventory(product.Id, lotAnchor.Id, lot1.Id, onHand: 5),
                TestData.Inventory(product.Id, skuAnchor.Id, lot2.Id, onHand: 5),
            ],
            [],
            [product]);

        var result = Strategy.CandidateLocations(product, lot1, context);

        // Only the same-lot location anchors, so its neighbour ranks first; the
        // other-lot location and its neighbour are merely distant candidates.
        result[0].Should().Be(nearLot.Id);
        result.Should().ContainInOrder(nearLot.Id, nearSku.Id);
        result.Should().NotContain(lotAnchor.Id);
    }

    [Fact]
    public void Falls_back_to_same_sku_when_no_location_holds_the_lot()
    {
        var product = TestData.Product();
        var incomingLot = TestData.Lot(product.Id, "LOT-NEW");
        var existingLot = TestData.Lot(product.Id, "LOT-OLD");

        var skuAnchor = At("Z1", "A1", "R1", "S1", "B5", "SKU-ANCHOR");
        var neighbour = At("Z1", "A1", "R1", "S1", "B6", "NEIGHBOUR");

        var context = new PutawayPlanContext(
            [skuAnchor, neighbour],
            [TestData.Inventory(product.Id, skuAnchor.Id, existingLot.Id, onHand: 5)],
            [],
            [product]);

        var result = Strategy.CandidateLocations(product, incomingLot, context);

        result.Should().Equal(neighbour.Id);
    }
}
