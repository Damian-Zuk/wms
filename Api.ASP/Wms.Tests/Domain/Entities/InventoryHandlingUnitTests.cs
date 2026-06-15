using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class InventoryHandlingUnitTests
{
    public class RelocateWith
    {
        [Fact]
        public void Moves_the_row_and_raises_a_moved_event_per_on_hand_stock()
        {
            var huId = Guid.NewGuid();
            var source = Guid.NewGuid();
            var destination = Guid.NewGuid();
            var receivedAt = DateTime.UtcNow.AddDays(-3);
            var inv = TestData.Inventory(
                Guid.NewGuid(), source, onHand: 8, receivedAt: receivedAt, handlingUnitId: huId);
            var moveId = Guid.NewGuid();

            var result = inv.RelocateWith(destination, moveId);

            result.IsSuccess.Should().BeTrue();
            inv.LocationId.Should().Be(destination);
            inv.ReceivedAt.Should().Be(receivedAt, "FIFO age travels with the pallet");

            var moved = inv.DomainEvents.OfType<HandlingUnitMovedDomainEvent>().Single();
            moved.MoveId.Should().Be(moveId);
            moved.HandlingUnitId.Should().Be(huId);
            moved.SourceLocationId.Should().Be(source);
            moved.DestinationLocationId.Should().Be(destination);
            moved.Quantity.Should().Be(8);
        }

        [Fact]
        public void Raises_no_event_for_an_empty_row()
        {
            var inv = TestData.Inventory(
                Guid.NewGuid(), Guid.NewGuid(), handlingUnitId: Guid.NewGuid());

            var result = inv.RelocateWith(Guid.NewGuid(), Guid.NewGuid());

            result.IsSuccess.Should().BeTrue();
            inv.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public void Fails_when_the_row_has_reserved_stock()
        {
            var source = Guid.NewGuid();
            var inv = TestData.Inventory(
                Guid.NewGuid(), source, onHand: 5, handlingUnitId: Guid.NewGuid());
            inv.Reserve(new Quantity(2));

            var result = inv.RelocateWith(Guid.NewGuid(), Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("HandlingUnit.HasReservedStock");
            inv.LocationId.Should().Be(source);
        }

        [Fact]
        public void Fails_when_the_destination_is_the_current_location()
        {
            var source = Guid.NewGuid();
            var inv = TestData.Inventory(
                Guid.NewGuid(), source, onHand: 5, handlingUnitId: Guid.NewGuid());

            var result = inv.RelocateWith(source, Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("HandlingUnit.SameLocation");
        }
    }

    public class Rebucket
    {
        [Fact]
        public void Moves_available_stock_between_loose_and_handling_unit_rows()
        {
            var productId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var receivedAt = DateTime.UtcNow.AddDays(-2);
            var loose = TestData.Inventory(productId, locationId, onHand: 10, receivedAt: receivedAt);
            var onHu = TestData.Inventory(productId, locationId, handlingUnitId: Guid.NewGuid());

            var result = loose.Rebucket(onHu, new Quantity(4));

            result.IsSuccess.Should().BeTrue();
            loose.OnHand.Value.Should().Be(6);
            onHu.OnHand.Value.Should().Be(4);
            onHu.ReceivedAt.Should().Be(receivedAt, "the destination inherits the source age when empty");
        }

        [Fact]
        public void Keeps_the_destination_age_when_it_already_has_one()
        {
            var productId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var older = DateTime.UtcNow.AddDays(-9);
            var newer = DateTime.UtcNow.AddDays(-1);
            var source = TestData.Inventory(productId, locationId, onHand: 5, receivedAt: newer);
            var destination = TestData.Inventory(
                productId, locationId, onHand: 5, receivedAt: older, handlingUnitId: Guid.NewGuid());

            source.Rebucket(destination, new Quantity(2));

            destination.ReceivedAt.Should().Be(older);
        }

        [Fact]
        public void Only_available_stock_may_move()
        {
            var productId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var source = TestData.Inventory(productId, locationId, onHand: 5);
            source.Reserve(new Quantity(3));
            var destination = TestData.Inventory(productId, locationId, handlingUnitId: Guid.NewGuid());

            var result = source.Rebucket(destination, new Quantity(3));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");
            source.OnHand.Value.Should().Be(5);
        }

        [Fact]
        public void Fails_across_locations()
        {
            var productId = Guid.NewGuid();
            var source = TestData.Inventory(productId, Guid.NewGuid(), onHand: 5);
            var destination = TestData.Inventory(productId, Guid.NewGuid(), handlingUnitId: Guid.NewGuid());

            var result = source.Rebucket(destination, new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("HandlingUnit.SameLocation");
        }

        [Fact]
        public void Fails_when_product_or_lot_differs()
        {
            var locationId = Guid.NewGuid();
            var source = TestData.Inventory(Guid.NewGuid(), locationId, onHand: 5);
            var destination = TestData.Inventory(Guid.NewGuid(), locationId, handlingUnitId: Guid.NewGuid());

            var result = source.Rebucket(destination, new Quantity(1));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.RebucketMustMatchProductAndLot");
        }
    }
}
