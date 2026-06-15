using FluentAssertions;
using Wms.Application.Picking;
using Wms.Application.Picking.Strategies;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Picking;

/// <summary>
/// Pure unit tests for the picking planner over an in-memory snapshot (no
/// database). The planner resolves the line's single strategy, ranks the
/// product's sources with it, and greedily takes down that ranking, committing
/// each take so sibling lines can't reuse the same units.
/// </summary>
public class PickingPlannerTests
{
    private static PickingPlanner NewPlanner() => new(
    [
        new FefoAllocationStrategy(),
        new FifoAllocationStrategy(),
        new LifoAllocationStrategy(),
        new LeastQuantityAllocationStrategy()
    ]);

    [Fact]
    public void Fefo_allocates_from_earliest_expiring_lot_first()
    {
        var product = TestData.Product("FEFO-1");
        var location = TestData.Location("FEFO-LOC");

        var early = TestData.Lot(product.Id, "EARLY", new DateOnly(2026, 06, 01));
        var middle = TestData.Lot(product.Id, "MIDDLE", new DateOnly(2026, 09, 01));
        var late = TestData.Lot(product.Id, "LATE", new DateOnly(2026, 12, 01));

        var invEarly = TestData.Inventory(product.Id, location.Id, early.Id, onHand: 10);
        var invMiddle = TestData.Inventory(product.Id, location.Id, middle.Id, onHand: 10);
        var invLate = TestData.Inventory(product.Id, location.Id, late.Id, onHand: 10);

        var context = new PickingContext([invEarly, invMiddle, invLate], [early, middle, late], [location]);

        // Request 15: drain the earliest lot (10) then take 5 from the next.
        var result = NewPlanner().Plan(product.Id, PickingStrategyType.Fefo, new Quantity(15), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].LotId.Should().Be(early.Id);
        result.Value[0].Quantity.Should().Be(10);
        result.Value[1].LotId.Should().Be(middle.Id);
        result.Value[1].Quantity.Should().Be(5);
        result.Value.Should().OnlyContain(a => a.Strategy == PickingStrategyType.Fefo);
    }

    [Fact]
    public void Fefo_sorts_lots_without_expiry_last()
    {
        var product = TestData.Product("FEFO-2");
        var location = TestData.Location("FEFO-LOC-2");

        var noExpiry = TestData.Lot(product.Id, "NO-EXP", expirationDate: null);
        var withExpiry = TestData.Lot(product.Id, "EXP", new DateOnly(2030, 01, 01));

        var invNoExpiry = TestData.Inventory(product.Id, location.Id, noExpiry.Id, onHand: 5);
        var invWithExpiry = TestData.Inventory(product.Id, location.Id, withExpiry.Id, onHand: 5);

        var context = new PickingContext([invNoExpiry, invWithExpiry], [noExpiry, withExpiry], [location]);

        var result = NewPlanner().Plan(product.Id, PickingStrategyType.Fefo, new Quantity(7), context);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].LotId.Should().Be(withExpiry.Id);
        result.Value[1].LotId.Should().Be(noExpiry.Id);
    }

    [Fact]
    public void Fifo_allocates_from_earliest_received_stock_first()
    {
        var product = TestData.Product("FIFO-1");
        // Two locations so each holds its own lotless row with a distinct received
        // date; FIFO must drain the oldest receipt first.
        var oldLoc = TestData.Location("FIFO-OLD");
        var newLoc = TestData.Location("FIFO-NEW");

        var oldInv = TestData.Inventory(product.Id, oldLoc.Id, lotId: null, onHand: 10,
            receivedAt: new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc));
        var newInv = TestData.Inventory(product.Id, newLoc.Id, lotId: null, onHand: 10,
            receivedAt: new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc));

        var context = new PickingContext([oldInv, newInv], [], [oldLoc, newLoc]);

        var result = NewPlanner().Plan(product.Id, PickingStrategyType.Fifo, new Quantity(15), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].LocationId.Should().Be(oldLoc.Id);
        result.Value[0].Quantity.Should().Be(10);
        result.Value[1].LocationId.Should().Be(newLoc.Id);
        result.Value[1].Quantity.Should().Be(5);
        result.Value.Should().OnlyContain(a => a.Strategy == PickingStrategyType.Fifo);
    }

    [Fact]
    public void Lifo_allocates_from_most_recently_received_stock_first()
    {
        var product = TestData.Product("LIFO-1");
        // Two locations so each holds its own lotless row with a distinct received
        // date; LIFO must drain the newest receipt first.
        var oldLoc = TestData.Location("LIFO-OLD");
        var newLoc = TestData.Location("LIFO-NEW");

        var oldInv = TestData.Inventory(product.Id, oldLoc.Id, lotId: null, onHand: 10,
            receivedAt: new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc));
        var newInv = TestData.Inventory(product.Id, newLoc.Id, lotId: null, onHand: 10,
            receivedAt: new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc));

        var context = new PickingContext([oldInv, newInv], [], [oldLoc, newLoc]);

        var result = NewPlanner().Plan(product.Id, PickingStrategyType.Lifo, new Quantity(15), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].LocationId.Should().Be(newLoc.Id);
        result.Value[0].Quantity.Should().Be(10);
        result.Value[1].LocationId.Should().Be(oldLoc.Id);
        result.Value[1].Quantity.Should().Be(5);
        result.Value.Should().OnlyContain(a => a.Strategy == PickingStrategyType.Lifo);
    }

    [Fact]
    public void LeastQuantity_allocates_from_the_smallest_holding_first()
    {
        var product = TestData.Product("LQ-1");
        var location = TestData.Location("LQ-LOC");

        var small = TestData.Lot(product.Id, "SMALL", new DateOnly(2026, 06, 01));
        var medium = TestData.Lot(product.Id, "MEDIUM", new DateOnly(2026, 06, 01));
        var large = TestData.Lot(product.Id, "LARGE", new DateOnly(2026, 06, 01));

        var invSmall = TestData.Inventory(product.Id, location.Id, small.Id, onHand: 2);
        var invMedium = TestData.Inventory(product.Id, location.Id, medium.Id, onHand: 5);
        var invLarge = TestData.Inventory(product.Id, location.Id, large.Id, onHand: 10);

        var context = new PickingContext([invLarge, invMedium, invSmall], [small, medium, large], [location]);

        // Request 6: drain the smallest holding (2) then take 4 from the next-smallest.
        var result = NewPlanner().Plan(product.Id, PickingStrategyType.LeastQuantity, new Quantity(6), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].LotId.Should().Be(small.Id);
        result.Value[0].Quantity.Should().Be(2);
        result.Value[1].LotId.Should().Be(medium.Id);
        result.Value[1].Quantity.Should().Be(4);
        result.Value.Should().OnlyContain(a => a.Strategy == PickingStrategyType.LeastQuantity);
    }

    [Fact]
    public void Fails_when_total_available_is_below_requested()
    {
        var product = TestData.Product("FEFO-3");
        var location = TestData.Location("FEFO-LOC-3");
        var lot = TestData.Lot(product.Id, "L1", new DateOnly(2026, 06, 01));
        var inv = TestData.Inventory(product.Id, location.Id, lot.Id, onHand: 3);

        var context = new PickingContext([inv], [lot], [location]);

        var result = NewPlanner().Plan(product.Id, PickingStrategyType.Fefo, new Quantity(10), context);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Picking.CannotPickFullQuantity");
    }

    [Fact]
    public void Commits_takes_so_a_second_line_cannot_reuse_the_same_units()
    {
        var product = TestData.Product("FEFO-4");
        var location = TestData.Location("FEFO-LOC-4");
        var lot = TestData.Lot(product.Id, "L1", new DateOnly(2026, 06, 01));
        var inv = TestData.Inventory(product.Id, location.Id, lot.Id, onHand: 10);

        var context = new PickingContext([inv], [lot], [location]);
        var planner = NewPlanner();

        var first = planner.Plan(product.Id, PickingStrategyType.Fefo, new Quantity(7), context);
        first.IsSuccess.Should().BeTrue();
        first.Value.Single().Quantity.Should().Be(7);

        // Only 3 units remain in the shared snapshot; a second line of 5 can't be met.
        var second = planner.Plan(product.Id, PickingStrategyType.Fefo, new Quantity(5), context);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Picking.CannotPickFullQuantity");
    }

    [Fact]
    public void Unknown_strategy_is_rejected()
    {
        var context = new PickingContext([], [], []);

        // A planner with no strategy registered for the requested type.
        var planner = new PickingPlanner([new FefoAllocationStrategy()]);
        var result = planner.Plan(Guid.NewGuid(), PickingStrategyType.Fifo, new Quantity(1), context);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Picking.UnknownStrategy");
    }

    [Fact]
    public void Handling_unit_and_loose_rows_at_one_location_are_distinct_sources()
    {
        var product = TestData.Product("HU-SPLIT");
        var location = TestData.Location("HU-SPLIT-LOC");
        var handlingUnitId = Guid.NewGuid();

        // Same product, same location, no lots: an older pallet and newer loose stock.
        var huRow = TestData.Inventory(product.Id, location.Id, onHand: 6,
            receivedAt: new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            handlingUnitId: handlingUnitId);
        var looseRow = TestData.Inventory(product.Id, location.Id, onHand: 6,
            receivedAt: new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc));

        var context = new PickingContext([huRow, looseRow], [], [location]);

        var result = NewPlanner().Plan(product.Id, PickingStrategyType.Fifo, new Quantity(9), context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].HandlingUnitId.Should().Be(handlingUnitId, "the pallet is older, FIFO drains it first");
        result.Value[0].Quantity.Should().Be(6);
        result.Value[1].HandlingUnitId.Should().BeNull();
        result.Value[1].Quantity.Should().Be(3);
    }

    [Fact]
    public void Commit_only_consumes_the_matching_handling_unit_bucket()
    {
        var product = TestData.Product("HU-COMMIT");
        var location = TestData.Location("HU-COMMIT-LOC");
        var handlingUnitId = Guid.NewGuid();

        var huRow = TestData.Inventory(product.Id, location.Id, onHand: 5, handlingUnitId: handlingUnitId);
        var looseRow = TestData.Inventory(product.Id, location.Id, onHand: 5);

        var context = new PickingContext([huRow, looseRow], [], [location]);

        context.Commit(product.Id, location.Id, null, handlingUnitId, 5);

        var remaining = context.AvailableFor(product.Id);
        remaining.Should().ContainSingle()
            .Which.HandlingUnitId.Should().BeNull("only the pallet bucket was committed");
    }
}
