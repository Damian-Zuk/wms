using FluentAssertions;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class HandlingUnitTests
{
    public class PlaceAt
    {
        [Fact]
        public void Pins_an_unplaced_unit_to_the_location()
        {
            var hu = TestData.HandlingUnit();
            var locationId = Guid.NewGuid();

            var result = hu.PlaceAt(locationId);

            result.IsSuccess.Should().BeTrue();
            hu.LocationId.Should().Be(locationId);
            hu.IsPlaced.Should().BeTrue();
        }

        [Fact]
        public void Is_idempotent_for_the_same_location()
        {
            var locationId = Guid.NewGuid();
            var hu = TestData.HandlingUnit(locationId: locationId);

            var result = hu.PlaceAt(locationId);

            result.IsSuccess.Should().BeTrue();
            hu.LocationId.Should().Be(locationId);
        }

        [Fact]
        public void Fails_when_already_placed_elsewhere()
        {
            var current = Guid.NewGuid();
            var hu = TestData.HandlingUnit(locationId: current);

            var result = hu.PlaceAt(Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("HandlingUnit.AlreadyPlacedElsewhere");
            hu.LocationId.Should().Be(current);
        }
    }

    public class MoveTo
    {
        [Fact]
        public void Relocates_a_placed_unit()
        {
            var hu = TestData.HandlingUnit(locationId: Guid.NewGuid());
            var destination = Guid.NewGuid();

            var result = hu.MoveTo(destination);

            result.IsSuccess.Should().BeTrue();
            hu.LocationId.Should().Be(destination);
        }

        [Fact]
        public void Fails_when_the_unit_was_never_placed()
        {
            var hu = TestData.HandlingUnit();

            var result = hu.MoveTo(Guid.NewGuid());

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("HandlingUnit.NotPlaced");
        }

        [Fact]
        public void Fails_when_the_destination_is_the_current_location()
        {
            var locationId = Guid.NewGuid();
            var hu = TestData.HandlingUnit(locationId: locationId);

            var result = hu.MoveTo(locationId);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("HandlingUnit.SameLocation");
        }
    }
}
