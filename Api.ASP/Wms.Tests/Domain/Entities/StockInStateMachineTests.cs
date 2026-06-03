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
        [InlineData(StockInStatus.Receiving)]
        [InlineData(StockInStatus.Received)]
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
                [(Guid.NewGuid(), 7), (Guid.NewGuid(), 3)],
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
                [(Guid.NewGuid(), 9)],
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
                [(Guid.NewGuid(), 10)],
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
            stockIn.StartReceiving();

            var result = stockIn.ModifyLinePlacements(
                lineId,
                [(Guid.NewGuid(), 10)],
                "alice",
                DateTime.UtcNow);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.CannotModifyItems");
        }
    }

    public class StartReceiving
    {
        [Fact]
        public void Draft_transitions_to_receiving()
        {
            var stockIn = NewStockInWithLine();

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
            var stockIn = NewStockInWithLine();
            DriveTo(stockIn, startStatus);

            var result = stockIn.StartReceiving();

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
        }
    }

    public class Receive
    {
        [Fact]
        public void Receiving_transitions_to_received_and_raises_one_event_per_placement()
        {
            var stockIn = new StockIn(Guid.NewGuid());
            stockIn.AddLineWithPlacements(
                Guid.NewGuid(),
                null,
                new Quantity(9),
                [new(Guid.NewGuid(), 2, PutawayStrategyType.NearestEmpty), new(Guid.NewGuid(), 7, PutawayStrategyType.NearestEmpty)]);
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
            var stockIn = NewStockInWithLine();
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
            var stockIn = NewStockInWithLine();
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
        [InlineData(StockInStatus.Receiving)]
        public void Allowed_from_draft_and_receiving(StockInStatus startStatus)
        {
            var stockIn = NewStockInWithLine();
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
