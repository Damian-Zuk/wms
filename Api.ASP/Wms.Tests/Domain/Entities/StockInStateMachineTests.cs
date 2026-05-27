using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class StockInStateMachineTests
{
    private static StockIn NewStockInWithItem(int quantity = 5)
    {
        var s = new StockIn(Guid.NewGuid());
        s.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(quantity));
        return s;
    }

    public class AddItem
    {
        [Fact]
        public void Allowed_in_draft()
        {
            var stockIn = new StockIn(Guid.NewGuid());

            var result = stockIn.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(1));

            result.IsSuccess.Should().BeTrue();
            stockIn.Items.Should().HaveCount(1);
        }

        [Theory]
        [InlineData(StockInStatus.Receiving)]
        [InlineData(StockInStatus.Received)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_draft(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithItem();
            DriveTo(stockIn, startStatus);

            var result = stockIn.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.CannotModifyItems");
        }
    }

    public class StartReceiving
    {
        [Fact]
        public void Draft_transitions_to_receiving()
        {
            var stockIn = NewStockInWithItem();

            var result = stockIn.StartReceiving();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Receiving);
        }

        [Theory]
        [InlineData(StockInStatus.Receiving)]
        [InlineData(StockInStatus.Received)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_draft(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithItem();
            DriveTo(stockIn, startStatus);

            var result = stockIn.StartReceiving();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
        }
    }

    public class Receive
    {
        [Fact]
        public void Receiving_transitions_to_received_and_raises_one_event_per_item()
        {
            var stockIn = new StockIn(Guid.NewGuid());
            stockIn.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(2));
            stockIn.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, new Quantity(7));
            stockIn.StartReceiving();
            stockIn.ClearDomainEvents();

            var result = stockIn.Receive();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Received);
            stockIn.DomainEvents
                .OfType<StockInItemReceivedDomainEvent>()
                .Should().HaveCount(2);
        }

        [Theory]
        [InlineData(StockInStatus.Draft)]
        [InlineData(StockInStatus.Received)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_receiving(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithItem();
            DriveTo(stockIn, startStatus);
            stockIn.ClearDomainEvents();

            var result = stockIn.Receive();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
            stockIn.DomainEvents.Should().BeEmpty();
        }
    }

    public class Complete
    {
        [Fact]
        public void Received_transitions_to_completed()
        {
            var stockIn = NewStockInWithItem();
            DriveTo(stockIn, StockInStatus.Received);

            var result = stockIn.Complete();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Completed);
        }

        [Theory]
        [InlineData(StockInStatus.Draft)]
        [InlineData(StockInStatus.Receiving)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_outside_received(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithItem();
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
        [InlineData(StockInStatus.Receiving)]
        public void Allowed_from_draft_and_receiving(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithItem();
            DriveTo(stockIn, startStatus);

            var result = stockIn.Cancel();

            result.IsSuccess.Should().BeTrue();
            stockIn.Status.Should().Be(StockInStatus.Cancelled);
        }

        [Theory]
        [InlineData(StockInStatus.Received)]
        [InlineData(StockInStatus.Completed)]
        [InlineData(StockInStatus.Cancelled)]
        public void Rejected_after_received(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithItem();
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
            case StockInStatus.Receiving:
                stockIn.StartReceiving();
                return;
            case StockInStatus.Received:
                stockIn.StartReceiving();
                stockIn.Receive();
                return;
            case StockInStatus.Completed:
                stockIn.StartReceiving();
                stockIn.Receive();
                stockIn.Complete();
                return;
            case StockInStatus.Cancelled:
                stockIn.Cancel();
                return;
        }
    }
}
