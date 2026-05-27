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
