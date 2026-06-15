using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class StockInStateMachineTests
{
    private static StockIn NewStockInWithLine(int quantity = 5)
    {
        var s = new StockIn(Guid.NewGuid());
        s.AddLineWithPlacements(
            Guid.NewGuid(),
            null,
            new Quantity(quantity),
            [new(Guid.NewGuid(), quantity, PutawayStrategyType.NearestEmpty)]);
        return s;
    }

    public class AddLineWithPlacements
    {
        [Fact]
        public void Allowed_in_draft()
        {
            var stockIn = new StockIn(Guid.NewGuid());

            var result = stockIn.AddLineWithPlacements(
                Guid.NewGuid(),
                null,
                new Quantity(10),
                [new(Guid.NewGuid(), 6, PutawayStrategyType.NearestEmpty), new(Guid.NewGuid(), 4, PutawayStrategyType.ConsolidateSameSku)]);

            result.IsSuccess.Should().BeTrue();
            stockIn.Lines.Should().HaveCount(1);
            stockIn.Lines.Single().Items.Should().HaveCount(2);
        }

        [Fact]
        public void Rejected_when_placements_do_not_match_line_total()
        {
            var stockIn = new StockIn(Guid.NewGuid());

            var result = stockIn.AddLineWithPlacements(
                Guid.NewGuid(),
                null,
                new Quantity(10),
                [new(Guid.NewGuid(), 6, PutawayStrategyType.NearestEmpty)]);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.PlacementsDoNotMatchLineTotal");
            stockIn.Lines.Should().BeEmpty();
        }

        [Theory]
        [InlineData(StockInStatus.Putaway)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_draft(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithLine();
            DriveTo(stockIn, startStatus);

            var result = stockIn.AddLineWithPlacements(
                Guid.NewGuid(),
                null,
                new Quantity(1),
                [new(Guid.NewGuid(), 1, PutawayStrategyType.NearestEmpty)]);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.CannotModifyItems");
        }
    }

    public class ModifyLinePlacements
    {
        [Fact]
        public void Replaces_placements_marks_manual_and_records_modifier()
        {
            var stockIn = NewStockInWithLine(quantity: 10);
            var lineId = stockIn.Lines.Single().Id;
            var when = DateTime.UtcNow;

            var result = stockIn.ModifyLinePlacements(
                lineId,
                [(Guid.NewGuid(), 7, null), (Guid.NewGuid(), 3, null)],
                "alice",
                when);

            result.IsSuccess.Should().BeTrue();
            var line = stockIn.Lines.Single();
            line.Items.Should().HaveCount(2);
            line.Items.Should().OnlyContain(i => i.Strategy == PutawayStrategyType.Manual);
            line.PlacedTotal.Should().Be(10);
            stockIn.ModifiedBy.Should().Be("alice");
            stockIn.ModifiedAt.Should().Be(when);
        }

        [Fact]
        public void Rejected_when_total_changes()
        {
            var stockIn = NewStockInWithLine(quantity: 10);
            var lineId = stockIn.Lines.Single().Id;

            var result = stockIn.ModifyLinePlacements(
                lineId,
                [(Guid.NewGuid(), 9, null)],
                "alice",
                DateTime.UtcNow);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.PlacementsDoNotMatchLineTotal");
        }

        [Fact]
        public void Rejected_for_unknown_line()
        {
            var stockIn = NewStockInWithLine(quantity: 10);

            var result = stockIn.ModifyLinePlacements(
                Guid.NewGuid(),
                [(Guid.NewGuid(), 10, null)],
                "alice",
                DateTime.UtcNow);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.LineNotFound");
        }

        [Fact]
        public void Rejected_outside_draft()
        {
            var stockIn = NewStockInWithLine(quantity: 10);
            var lineId = stockIn.Lines.Single().Id;
            stockIn.StartPutaway();

            var result = stockIn.ModifyLinePlacements(
                lineId,
                [(Guid.NewGuid(), 10, null)],
                "alice",
                DateTime.UtcNow);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.CannotModifyItems");
        }
    }

    public class StartPutaway
    {
        [Fact]
        public void Draft_transitions_to_putaway()
        {
            var stockIn = NewStockInWithLine();

            var result = stockIn.StartPutaway();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Putaway);
        }

        [Theory]
        [InlineData(StockInStatus.Putaway)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_draft(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithLine();
            DriveTo(stockIn, startStatus);

            var result = stockIn.StartPutaway();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
        }
    }

    public class PutawayItem
    {
        [Fact]
        public void Partial_putaway_tracks_progress_keeps_status_and_raises_event()
        {
            var stockIn = NewStockInWithLine(quantity: 10);
            stockIn.StartPutaway();
            stockIn.ClearDomainEvents();
            var item = stockIn.Lines.Single().Items.Single();

            var result = stockIn.PutawayItem(item.Id, new Quantity(4));

            result.IsSuccess.Should().BeTrue();
            item.PlacedQuantity.Value.Should().Be(4);
            item.Remaining.Should().Be(6);
            item.IsFullyPlaced.Should().BeFalse();
            stockIn.Status.Should().Be(StockInStatus.Putaway);
            stockIn.DomainEvents
                .OfType<StockInItemPutawayDomainEvent>()
                .Should().ContainSingle()
                .Which.Quantity.Should().Be(4);
        }

        [Fact]
        public void Can_be_done_in_parts_until_fully_placed()
        {
            var stockIn = NewStockInWithLine(quantity: 10);
            stockIn.StartPutaway();
            var item = stockIn.Lines.Single().Items.Single();

            stockIn.PutawayItem(item.Id, new Quantity(4)).IsSuccess.Should().BeTrue();
            stockIn.PutawayItem(item.Id, new Quantity(6)).IsSuccess.Should().BeTrue();

            item.PlacedQuantity.Value.Should().Be(10);
            item.IsFullyPlaced.Should().BeTrue();
        }

        [Fact]
        public void Rejected_when_quantity_exceeds_remaining()
        {
            var stockIn = NewStockInWithLine(quantity: 10);
            stockIn.StartPutaway();
            var item = stockIn.Lines.Single().Items.Single();
            stockIn.PutawayItem(item.Id, new Quantity(7));

            var result = stockIn.PutawayItem(item.Id, new Quantity(4));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.PutawayQuantityExceedsRemaining");
        }

        [Fact]
        public void Rejected_for_unknown_item()
        {
            var stockIn = NewStockInWithLine();
            stockIn.StartPutaway();

            var result = stockIn.PutawayItem(Guid.NewGuid(), new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.ItemNotFound");
        }

        [Theory]
        [InlineData(StockInStatus.Draft)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_putaway(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithLine();
            DriveTo(stockIn, startStatus);
            var itemId = stockIn.Lines.Single().Items.Single().Id;
            stockIn.ClearDomainEvents();

            var result = stockIn.PutawayItem(itemId, new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.CannotPutaway");
            stockIn.DomainEvents.Should().BeEmpty();
        }
    }

    public class Complete
    {
        [Fact]
        public void Putaway_with_all_items_placed_transitions_to_completed()
        {
            var stockIn = NewStockInWithLine(quantity: 5);
            stockIn.StartPutaway();
            var item = stockIn.Lines.Single().Items.Single();
            stockIn.PutawayItem(item.Id, new Quantity(5));

            var result = stockIn.Complete();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Completed);
        }

        [Fact]
        public void Rejected_when_not_all_items_placed()
        {
            var stockIn = NewStockInWithLine(quantity: 5);
            stockIn.StartPutaway();
            var item = stockIn.Lines.Single().Items.Single();
            stockIn.PutawayItem(item.Id, new Quantity(3));

            var result = stockIn.Complete();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.NotAllItemsPlaced");
        }

        [Theory]
        [InlineData(StockInStatus.Draft)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_putaway(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithLine();
            DriveTo(stockIn, startStatus);

            var result = stockIn.Complete();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
        }
    }

    public class Cancel
    {
        [Theory]
        [InlineData(StockInStatus.Draft)]
        [InlineData(StockInStatus.Putaway)]
        public void Allowed_from_draft_and_putaway_and_records_phase(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithLine();
            DriveTo(stockIn, startStatus);

            var result = stockIn.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Cancelled);
            stockIn.CancelledFrom.Should().Be(startStatus);
        }

        [Fact]
        public void From_draft_does_not_raise_removal_events()
        {
            var stockIn = NewStockInWithLine();

            var result = stockIn.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockIn.DomainEvents.OfType<StockInItemRemovedFromStockDomainEvent>().Should().BeEmpty();
        }

        [Fact]
        public void From_putaway_with_no_placements_does_not_raise_removal_events()
        {
            var stockIn = NewStockInWithLine();
            stockIn.StartPutaway();
            stockIn.ClearDomainEvents();

            var result = stockIn.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockIn.DomainEvents.OfType<StockInItemRemovedFromStockDomainEvent>().Should().BeEmpty();
        }

        [Fact]
        public void From_putaway_raises_removal_event_for_each_placed_item()
        {
            var stockIn = new StockIn(Guid.NewGuid());
            stockIn.AddLineWithPlacements(Guid.NewGuid(), null, new Quantity(2),
                [new(Guid.NewGuid(), 2, PutawayStrategyType.NearestEmpty)]);
            stockIn.AddLineWithPlacements(Guid.NewGuid(), null, new Quantity(5),
                [new(Guid.NewGuid(), 5, PutawayStrategyType.NearestEmpty)]);
            stockIn.StartPutaway();
            foreach (var line in stockIn.Lines)
            {
                var item = line.Items.Single();
                stockIn.PutawayItem(item.Id, item.Quantity);
            }
            stockIn.ClearDomainEvents();

            var result = stockIn.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Cancelled);
            stockIn.DomainEvents
                .OfType<StockInItemRemovedFromStockDomainEvent>()
                .Should().HaveCount(2);
        }

        [Fact]
        public void From_putaway_uses_placed_quantity_and_skips_unplaced_items()
        {
            // Two lines but only one is partially put away; the cancel removes only
            // those placed units and skips the line that never started.
            var stockIn = new StockIn(Guid.NewGuid());
            stockIn.AddLineWithPlacements(Guid.NewGuid(), null, new Quantity(5),
                [new(Guid.NewGuid(), 5, PutawayStrategyType.NearestEmpty)]);
            stockIn.AddLineWithPlacements(Guid.NewGuid(), null, new Quantity(2),
                [new(Guid.NewGuid(), 2, PutawayStrategyType.NearestEmpty)]);
            stockIn.StartPutaway();
            var placedItem = stockIn.Lines.First().Items.Single();
            stockIn.PutawayItem(placedItem.Id, new Quantity(3));
            stockIn.ClearDomainEvents();

            var result = stockIn.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockIn.DomainEvents
                .OfType<StockInItemRemovedFromStockDomainEvent>()
                .Should().ContainSingle()
                .Which.Quantity.Should().Be(3);
        }

        [Theory]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_after_putaway(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithLine();
            DriveTo(stockIn, startStatus);

            var result = stockIn.Cancel();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
        }
    }

    /// <summary>
    /// Walks a fresh-from-Draft StockIn forward to the requested status using
    /// only public domain methods. Cancelled is special-cased because it's
    /// reachable from two states.
    /// </summary>
    private static void DriveTo(StockIn stockIn, StockInStatus target)
    {
        switch (target)
        {
            case StockInStatus.Draft:
                return;
            case StockInStatus.Putaway:
                stockIn.StartPutaway();
                return;
            case StockInStatus.Completed:
                stockIn.StartPutaway();
                PutawayAll(stockIn);
                stockIn.Complete();
                return;
            case StockInStatus.Cancelled:
                stockIn.Cancel();
                return;
        }
    }

    private static void PutawayAll(StockIn stockIn)
    {
        foreach (var line in stockIn.Lines)
            foreach (var item in line.Items)
                stockIn.PutawayItem(item.Id, item.Quantity);
    }
}
