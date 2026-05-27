using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class InventoryTests
{
    private static Inventory NewInventory(int onHand = 0, int reserve = 0)
    {
        var inv = new Inventory(Guid.NewGuid(), Guid.NewGuid());
        if (onHand > 0)
            inv.Increase(new Quantity(onHand));
        if (reserve > 0)
            inv.Reserve(new Quantity(reserve));
        return inv;
    }

    public class Reserve
    {
        [Fact]
        public void Succeeds_when_available_covers_request()
        {
            var inv = NewInventory(onHand: 10);

            var result = inv.Reserve(new Quantity(7));

            result.IsSuccess.Should().BeTrue();
            inv.OnHand.Value.Should().Be(10);
            inv.Reserved.Value.Should().Be(7);
            inv.Available.Value.Should().Be(3);
        }

        [Fact]
        public void Stacks_on_previous_reservation()
        {
            var inv = NewInventory(onHand: 10, reserve: 4);

            var result = inv.Reserve(new Quantity(3));

            result.IsSuccess.Should().BeTrue();
            inv.Reserved.Value.Should().Be(7);
            inv.Available.Value.Should().Be(3);
        }

        [Fact]
        public void Fails_when_request_exceeds_available()
        {
            var inv = NewInventory(onHand: 5, reserve: 3);

            var result = inv.Reserve(new Quantity(3));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");
            inv.Reserved.Value.Should().Be(3);
        }

        [Fact]
        public void Fails_with_empty_inventory()
        {
            var inv = NewInventory();

            var result = inv.Reserve(new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");
        }
    }

    public class Pick
    {
        [Fact]
        public void Succeeds_when_amount_was_reserved()
        {
            var inv = NewInventory(onHand: 10, reserve: 6);

            var result = inv.Pick(new Quantity(4));

            result.IsSuccess.Should().BeTrue();
            inv.OnHand.Value.Should().Be(6);
            inv.Reserved.Value.Should().Be(2);
            inv.Available.Value.Should().Be(4);
        }

        [Fact]
        public void Fails_when_more_than_reserved_is_picked()
        {
            var inv = NewInventory(onHand: 10, reserve: 3);

            var result = inv.Pick(new Quantity(5));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.ReleaseExceedsReserved");
        }
    }

    public class ReleaseReservation
    {
        [Fact]
        public void Releases_full_reservation()
        {
            var inv = NewInventory(onHand: 10, reserve: 6);

            var result = inv.ReleaseReservation(new Quantity(6));

            result.IsSuccess.Should().BeTrue();
            inv.OnHand.Value.Should().Be(10);
            inv.Reserved.Value.Should().Be(0);
            inv.Available.Value.Should().Be(10);
        }

        [Fact]
        public void Releases_partial_reservation()
        {
            var inv = NewInventory(onHand: 10, reserve: 6);

            var result = inv.ReleaseReservation(new Quantity(4));

            result.IsSuccess.Should().BeTrue();
            inv.Reserved.Value.Should().Be(2);
            inv.Available.Value.Should().Be(8);
        }

        [Fact]
        public void Fails_when_release_exceeds_reserved()
        {
            var inv = NewInventory(onHand: 10, reserve: 3);

            var result = inv.ReleaseReservation(new Quantity(5));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.ReleaseExceedsReserved");
            inv.Reserved.Value.Should().Be(3);
        }

        [Fact]
        public void Fails_when_nothing_was_reserved()
        {
            var inv = NewInventory(onHand: 10);

            var result = inv.ReleaseReservation(new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.ReleaseExceedsReserved");
        }
    }

    public class TransferTo
    {
        private static Inventory NewSource(Guid productId, Guid sourceLocationId, int onHand = 0, int reserve = 0)
        {
            var inv = new Inventory(productId, sourceLocationId);
            if (onHand > 0) inv.Increase(new Quantity(onHand));
            if (reserve > 0) inv.Reserve(new Quantity(reserve));
            return inv;
        }

        private static Inventory NewDestination(Guid productId, Guid destinationLocationId, int onHand = 0)
        {
            var inv = new Inventory(productId, destinationLocationId);
            if (onHand > 0) inv.Increase(new Quantity(onHand));
            return inv;
        }

        [Fact]
        public void Happy_path_moves_on_hand_and_raises_event()
        {
            var productId = Guid.NewGuid();
            var sourceLocId = Guid.NewGuid();
            var destLocId = Guid.NewGuid();
            var source = NewSource(productId, sourceLocId, onHand: 10);
            var destination = NewDestination(productId, destLocId, onHand: 2);
            var transferId = Guid.NewGuid();

            var result = source.TransferTo(destination, new Quantity(4), transferId);

            result.IsSuccess.Should().BeTrue();
            source.OnHand.Value.Should().Be(6);
            destination.OnHand.Value.Should().Be(6);

            source.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<StockTransferredDomainEvent>()
                .Which.Should().Match<StockTransferredDomainEvent>(e =>
                    e.TransferId == transferId
                    && e.ProductId == productId
                    && e.SourceLocationId == sourceLocId
                    && e.DestinationLocationId == destLocId
                    && e.Quantity == 4);
        }

        [Fact]
        public void Same_source_and_destination_is_rejected()
        {
            var productId = Guid.NewGuid();
            var locId = Guid.NewGuid();
            var source = NewSource(productId, locId, onHand: 10);
            // Build a "destination" that is actually the same location.
            var alias = new Inventory(productId, locId);

            var result = source.TransferTo(alias, new Quantity(1), Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockTransfer.SameSourceAndDestination");
            source.OnHand.Value.Should().Be(10);
            source.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void Insufficient_available_is_rejected()
        {
            var productId = Guid.NewGuid();
            var source = NewSource(productId, Guid.NewGuid(), onHand: 10, reserve: 8);
            var destination = NewDestination(productId, Guid.NewGuid());

            // Available = 10 - 8 = 2. Requesting 5 must fail.
            var result = source.TransferTo(destination, new Quantity(5), Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");
            source.OnHand.Value.Should().Be(10);
            destination.OnHand.Value.Should().Be(0);
            source.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void Reserved_stays_pinned_at_source_and_does_not_move_to_destination()
        {
            // The reservation points at the source row; if we let physical
            // stock disappear while Reserved follows it, the owning pick flow
            // breaks. Reserved must remain unchanged on both sides.
            var productId = Guid.NewGuid();
            var source = NewSource(productId, Guid.NewGuid(), onHand: 10, reserve: 3);
            var destination = NewDestination(productId, Guid.NewGuid());

            var result = source.TransferTo(destination, new Quantity(5), Guid.NewGuid());

            result.IsSuccess.Should().BeTrue();
            source.OnHand.Value.Should().Be(5);
            source.Reserved.Value.Should().Be(3);
            source.Available.Value.Should().Be(2);
            destination.OnHand.Value.Should().Be(5);
            destination.Reserved.Value.Should().Be(0);
        }
    }

    public class Adjust
    {
        [Fact]
        public void Positive_adjustment_raises_inventory_adjusted_event()
        {
            var inv = NewInventory(onHand: 5);

            var result = inv.Adjust(3, reason: "found stock");

            result.IsSuccess.Should().BeTrue();
            inv.OnHand.Value.Should().Be(8);

            inv.DomainEvents.Should().ContainSingle()
                .Which.Should().BeOfType<InventoryAdjustedDomainEvent>()
                .Which.QuantityChange.Should().Be(3);
        }

        [Fact]
        public void Negative_adjustment_subtracts_from_on_hand()
        {
            var inv = NewInventory(onHand: 10);

            var result = inv.Adjust(-4, reason: "cycle count");

            result.IsSuccess.Should().BeTrue();
            inv.OnHand.Value.Should().Be(6);
            inv.DomainEvents.Should().HaveCount(1);
        }

        [Fact]
        public void Zero_change_is_rejected()
        {
            var inv = NewInventory(onHand: 5);

            var result = inv.Adjust(0, reason: null);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.AdjustmentMustBeNonZero");
            inv.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void Negative_adjustment_below_on_hand_is_rejected()
        {
            var inv = NewInventory(onHand: 3);

            var result = inv.Adjust(-5, reason: null);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientQuantity");
            inv.OnHand.Value.Should().Be(3);
            inv.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void Negative_adjustment_that_would_breach_reservation_is_rejected()
        {
            var inv = NewInventory(onHand: 10, reserve: 8);

            var result = inv.Adjust(-5, reason: null);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.AdjustmentWouldViolateReservation");
            inv.OnHand.Value.Should().Be(10);
            inv.Reserved.Value.Should().Be(8);
            inv.DomainEvents.Should().BeEmpty();
        }
    }
}
