using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Domain.Models;
using Wms.Domain.ValueObjects;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class StockOutStateMachineTests
{
    private const PickingStrategyType Strategy = PickingStrategyType.Fefo;

    private static StockOut NewStockOutWithLine(int quantity = 5)
    {
        var s = new StockOut(Guid.NewGuid());
        s.AddLineWithAllocations(
            Guid.NewGuid(),
            Strategy,
            new Quantity(quantity),
            [new PickAllocation(Guid.NewGuid(), null, quantity, Strategy)]);
        return s;
    }

    public class AddLineWithAllocations
    {
        [Fact]
        public void Allowed_in_draft()
        {
            var stockOut = new StockOut(Guid.NewGuid());

            var result = stockOut.AddLineWithAllocations(
                Guid.NewGuid(),
                Strategy,
                new Quantity(10),
                [new PickAllocation(Guid.NewGuid(), null, 6, Strategy), new PickAllocation(Guid.NewGuid(), null, 4, Strategy)]);

            result.IsSuccess.Should().BeTrue();
            stockOut.Lines.Should().HaveCount(1);
            stockOut.Lines.Single().Items.Should().HaveCount(2);
        }

        [Fact]
        public void Rejected_when_allocations_do_not_match_line_total()
        {
            var stockOut = new StockOut(Guid.NewGuid());

            var result = stockOut.AddLineWithAllocations(
                Guid.NewGuid(),
                Strategy,
                new Quantity(10),
                [new PickAllocation(Guid.NewGuid(), null, 6, Strategy)]);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.AllocationsDoNotMatchLineTotal");
            stockOut.Lines.Should().BeEmpty();
        }

        [Theory]
        [InlineData(StockOutStatus.Picking)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_draft(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithLine();
            DriveTo(stockOut, startStatus);

            var result = stockOut.AddLineWithAllocations(
                Guid.NewGuid(),
                Strategy,
                new Quantity(1),
                [new PickAllocation(Guid.NewGuid(), null, 1, Strategy)]);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.CannotModifyItems");
        }
    }

    public class StartPicking
    {
        [Fact]
        public void Draft_transitions_to_picking_without_raising_picked_events()
        {
            var stockOut = NewStockOutWithLine();

            var result = stockOut.StartPicking();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Picking);
            stockOut.DomainEvents.OfType<StockOutItemPickedDomainEvent>().Should().BeEmpty();
        }

        [Theory]
        [InlineData(StockOutStatus.Picking)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_draft(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithLine();
            DriveTo(stockOut, startStatus);
            stockOut.ClearDomainEvents();

            var result = stockOut.StartPicking();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    public class PickItem
    {
        [Fact]
        public void Partial_pick_tracks_progress_keeps_status_and_raises_event()
        {
            var stockOut = NewStockOutWithLine(quantity: 10);
            stockOut.StartPicking();
            stockOut.ClearDomainEvents();
            var item = stockOut.Lines.Single().Items.Single();

            var result = stockOut.PickItem(item.Id, new Quantity(4));

            result.IsSuccess.Should().BeTrue();
            item.PickedQuantity.Value.Should().Be(4);
            item.Remaining.Should().Be(6);
            item.IsFullyPicked.Should().BeFalse();
            stockOut.Status.Should().Be(StockOutStatus.Picking);
            stockOut.DomainEvents
                .OfType<StockOutItemPickedDomainEvent>()
                .Should().ContainSingle()
                .Which.Quantity.Should().Be(4);
        }

        [Fact]
        public void Can_be_done_in_parts_until_fully_picked()
        {
            var stockOut = NewStockOutWithLine(quantity: 10);
            stockOut.StartPicking();
            var item = stockOut.Lines.Single().Items.Single();

            stockOut.PickItem(item.Id, new Quantity(4)).IsSuccess.Should().BeTrue();
            stockOut.PickItem(item.Id, new Quantity(6)).IsSuccess.Should().BeTrue();

            item.PickedQuantity.Value.Should().Be(10);
            item.IsFullyPicked.Should().BeTrue();
        }

        [Fact]
        public void Rejected_when_quantity_exceeds_remaining()
        {
            var stockOut = NewStockOutWithLine(quantity: 10);
            stockOut.StartPicking();
            var item = stockOut.Lines.Single().Items.Single();
            stockOut.PickItem(item.Id, new Quantity(7));

            var result = stockOut.PickItem(item.Id, new Quantity(4));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.PickQuantityExceedsRemaining");
        }

        [Fact]
        public void Rejected_for_unknown_item()
        {
            var stockOut = NewStockOutWithLine();
            stockOut.StartPicking();

            var result = stockOut.PickItem(Guid.NewGuid(), new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.ItemNotFound");
        }

        [Theory]
        [InlineData(StockOutStatus.Draft)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_picking(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithLine();
            DriveTo(stockOut, startStatus);
            var itemId = stockOut.Lines.Single().Items.Single().Id;
            stockOut.ClearDomainEvents();

            var result = stockOut.PickItem(itemId, new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.CannotPick");
            stockOut.DomainEvents.Should().BeEmpty();
        }
    }

    public class Complete
    {
        [Fact]
        public void Picking_with_all_items_picked_transitions_to_completed()
        {
            var stockOut = NewStockOutWithLine(quantity: 5);
            stockOut.StartPicking();
            var item = stockOut.Lines.Single().Items.Single();
            stockOut.PickItem(item.Id, new Quantity(5));

            var result = stockOut.Complete();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Completed);
        }

        [Fact]
        public void Rejected_when_not_all_items_picked()
        {
            var stockOut = NewStockOutWithLine(quantity: 5);
            stockOut.StartPicking();
            var item = stockOut.Lines.Single().Items.Single();
            stockOut.PickItem(item.Id, new Quantity(3));

            var result = stockOut.Complete();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.NotAllItemsPicked");
        }

        [Theory]
        [InlineData(StockOutStatus.Draft)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_picking(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithLine();
            DriveTo(stockOut, startStatus);

            var result = stockOut.Complete();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    public class Cancel
    {
        [Theory]
        [InlineData(StockOutStatus.Draft)]
        [InlineData(StockOutStatus.Picking)]
        public void Allowed_from_draft_and_picking_and_records_phase(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithLine();
            DriveTo(stockOut, startStatus);
            stockOut.ClearDomainEvents();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Cancelled);
            stockOut.CancelledFrom.Should().Be(startStatus);
        }

        [Fact]
        public void From_draft_does_not_raise_return_events()
        {
            var stockOut = NewStockOutWithLine();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.DomainEvents.OfType<StockOutItemReturnedToStockDomainEvent>().Should().BeEmpty();
        }

        [Fact]
        public void From_picking_with_no_picks_does_not_raise_return_events()
        {
            var stockOut = NewStockOutWithLine();
            stockOut.StartPicking();
            stockOut.ClearDomainEvents();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.DomainEvents.OfType<StockOutItemReturnedToStockDomainEvent>().Should().BeEmpty();
        }

        [Fact]
        public void From_picking_raises_return_event_for_each_picked_item()
        {
            var stockOut = new StockOut(Guid.NewGuid());
            stockOut.AddLineWithAllocations(Guid.NewGuid(), Strategy, new Quantity(2),
                [new PickAllocation(Guid.NewGuid(), null, 2, Strategy)]);
            stockOut.AddLineWithAllocations(Guid.NewGuid(), Strategy, new Quantity(5),
                [new PickAllocation(Guid.NewGuid(), null, 5, Strategy)]);
            stockOut.StartPicking();
            foreach (var line in stockOut.Lines)
            {
                var item = line.Items.Single();
                stockOut.PickItem(item.Id, item.Quantity);
            }
            stockOut.ClearDomainEvents();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Cancelled);
            stockOut.DomainEvents
                .OfType<StockOutItemReturnedToStockDomainEvent>()
                .Should().HaveCount(2);
        }

        [Fact]
        public void From_picking_skips_items_with_no_picked_units()
        {
            // Two lines but only one item is picked; the cancel returns only it.
            var stockOut = new StockOut(Guid.NewGuid());
            stockOut.AddLineWithAllocations(Guid.NewGuid(), Strategy, new Quantity(2),
                [new PickAllocation(Guid.NewGuid(), null, 2, Strategy)]);
            stockOut.AddLineWithAllocations(Guid.NewGuid(), Strategy, new Quantity(5),
                [new PickAllocation(Guid.NewGuid(), null, 5, Strategy)]);
            stockOut.StartPicking();
            var pickedItem = stockOut.Lines.First().Items.Single();
            stockOut.PickItem(pickedItem.Id, new Quantity(2));
            stockOut.ClearDomainEvents();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.DomainEvents
                .OfType<StockOutItemReturnedToStockDomainEvent>()
                .Should().ContainSingle()
                .Which.Quantity.Should().Be(2);
        }

        [Theory]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_from_completed_or_cancelled(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithLine();
            DriveTo(stockOut, startStatus);

            var result = stockOut.Cancel();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    /// <summary>
    /// Walks a fresh-from-Draft StockOut forward to the requested status using
    /// only public domain methods. Cancelled is special-cased because it's
    /// reachable from two states.
    /// </summary>
    private static void DriveTo(StockOut stockOut, StockOutStatus target)
    {
        switch (target)
        {
            case StockOutStatus.Draft:
                return;
            case StockOutStatus.Picking:
                stockOut.StartPicking();
                return;
            case StockOutStatus.Completed:
                stockOut.StartPicking();
                PickAll(stockOut);
                stockOut.Complete();
                return;
            case StockOutStatus.Cancelled:
                stockOut.Cancel();
                return;
        }
    }

    private static void PickAll(StockOut stockOut)
    {
        foreach (var line in stockOut.Lines)
            foreach (var item in line.Items)
                stockOut.PickItem(item.Id, item.Quantity);
    }
}
