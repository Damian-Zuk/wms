using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class StockOutStateMachineTests
{
    private static StockOut NewStockOutWithItem(int quantity = 5)
    {
        var s = new StockOut(Guid.NewGuid());
        s.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(quantity));
        return s;
    }

    public class AddItem
    {
        [Fact]
        public void Allowed_in_draft()
        {
            var stockOut = new StockOut(Guid.NewGuid());

            var result = stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(1));

            result.IsSuccess.Should().BeTrue();
            stockOut.Items.Should().HaveCount(1);
        }

        [Theory]
        [InlineData(StockOutStatus.Picking)]
        [InlineData(StockOutStatus.Packed)]
        [InlineData(StockOutStatus.Shipped)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_draft(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, startStatus);

            var result = stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.CannotModifyItems");
        }
    }

    public class StartPicking
    {
        [Fact]
        public void Draft_transitions_to_picking_without_raising_picked_events()
        {
            var stockOut = new StockOut(Guid.NewGuid());
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(3));
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(7));

            var result = stockOut.StartPicking();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Picking);
            stockOut.DomainEvents.OfType<StockOutItemPickedDomainEvent>().Should().BeEmpty();
        }

        [Theory]
        [InlineData(StockOutStatus.Picking)]
        [InlineData(StockOutStatus.Packed)]
        [InlineData(StockOutStatus.Shipped)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_draft(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, startStatus);
            stockOut.ClearDomainEvents();

            var result = stockOut.StartPicking();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    public class Pack
    {
        [Fact]
        public void Picking_transitions_to_packed_and_raises_picked_event_per_item()
        {
            var stockOut = new StockOut(Guid.NewGuid());
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(3));
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(7));
            stockOut.StartPicking();
            stockOut.ClearDomainEvents();

            var result = stockOut.Pack();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Packed);
            stockOut.DomainEvents.OfType<StockOutItemPickedDomainEvent>().Should().HaveCount(2);
        }

        [Theory]
        [InlineData(StockOutStatus.Draft)]
        [InlineData(StockOutStatus.Packed)]
        [InlineData(StockOutStatus.Shipped)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_picking(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, startStatus);

            var result = stockOut.Pack();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    public class Ship
    {
        [Fact]
        public void Packed_transitions_to_shipped()
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, StockOutStatus.Packed);

            var result = stockOut.Ship();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Shipped);
        }

        [Theory]
        [InlineData(StockOutStatus.Draft)]
        [InlineData(StockOutStatus.Picking)]
        [InlineData(StockOutStatus.Shipped)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_packed(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, startStatus);

            var result = stockOut.Ship();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    public class Complete
    {
        [Fact]
        public void Shipped_transitions_to_completed()
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, StockOutStatus.Shipped);

            var result = stockOut.Complete();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Completed);
        }

        [Theory]
        [InlineData(StockOutStatus.Draft)]
        [InlineData(StockOutStatus.Picking)]
        [InlineData(StockOutStatus.Packed)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_outside_shipped(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, startStatus);

            var result = stockOut.Complete();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    public class Cancel
    {
        [Fact]
        public void From_draft_does_not_raise_return_events()
        {
            var stockOut = NewStockOutWithItem();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Cancelled);
            stockOut.DomainEvents.OfType<StockOutItemReturnedToStockDomainEvent>().Should().BeEmpty();
        }

        [Fact]
        public void From_picking_does_not_raise_return_events()
        {
            var stockOut = new StockOut(Guid.NewGuid());
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(2));
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(5));
            DriveTo(stockOut, StockOutStatus.Picking);
            stockOut.ClearDomainEvents();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Cancelled);
            stockOut.DomainEvents.OfType<StockOutItemReturnedToStockDomainEvent>().Should().BeEmpty();
        }

        [Fact]
        public void From_packed_raises_return_event_per_item()
        {
            var stockOut = new StockOut(Guid.NewGuid());
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(2));
            stockOut.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(5));
            DriveTo(stockOut, StockOutStatus.Packed);
            stockOut.ClearDomainEvents();

            var result = stockOut.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockOut.Status.Should().Be(StockOutStatus.Cancelled);
            stockOut.DomainEvents
                .OfType<StockOutItemReturnedToStockDomainEvent>()
                .Should().HaveCount(2);
        }

        [Theory]
        [InlineData(StockOutStatus.Shipped)]
        [InlineData(StockOutStatus.Completed)]
        [InlineData(StockOutStatus.Cancelled)]
        public void Rejected_from_shipped_or_later(StockOutStatus startStatus)
        {
            var stockOut = NewStockOutWithItem();
            DriveTo(stockOut, startStatus);

            var result = stockOut.Cancel();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }
    }

    /// <summary>
    /// Walks a fresh-from-Draft StockOut forward to the requested status
    /// using only public domain methods.
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
            case StockOutStatus.Packed:
                stockOut.StartPicking();
                stockOut.Pack();
                return;
            case StockOutStatus.Shipped:
                stockOut.StartPicking();
                stockOut.Pack();
                stockOut.Ship();
                return;
            case StockOutStatus.Completed:
                stockOut.StartPicking();
                stockOut.Pack();
                stockOut.Ship();
                stockOut.Complete();
                return;
            case StockOutStatus.Cancelled:
                stockOut.Cancel();
                return;
        }
    }
}
