using FluentAssertions;
using Wms.Application.Handlers.Locations.Queries;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Locations;

public class GetLocationQueryHandlerTests : IntegrationTestBase
{
    public GetLocationQueryHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Reports_all_three_capacity_limits_and_their_occupancy()
    {
        var ct = TestContext.Current.CancellationToken;

        // 10 units on hand of a 2 kg / 1.5 dm³ product => 20 kg, 15 dm³ occupied.
        var product = TestData.Product("GL-1", weight: 2m, volume: 1.5m);
        var location = TestData.Location("GL-1-LOC", capacity: 100, weightCapacity: 50m, volumeCapacity: 40m);
        var inventory = TestData.Inventory(product.Id, location.Id, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inventory);
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new GetLocationQueryHandler(queryContext);

        var result = await handler.Handle(new GetLocationQuery(location.Id), ct);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Capacity.Should().Be(100);
        dto.Occupancy.Should().Be(10);
        dto.WeightCapacity.Should().Be(50m);
        dto.WeightOccupancy.Should().Be(20m);
        dto.VolumeCapacity.Should().Be(40m);
        dto.VolumeOccupancy.Should().Be(15m);
    }

    [Fact]
    public async Task Unlimited_location_reports_null_limits_with_zero_occupancy()
    {
        var ct = TestContext.Current.CancellationToken;

        var location = TestData.Location("GL-2-LOC");

        Context.Locations.Add(location);
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new GetLocationQueryHandler(queryContext);

        var result = await handler.Handle(new GetLocationQuery(location.Id), ct);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Capacity.Should().BeNull();
        dto.WeightCapacity.Should().BeNull();
        dto.VolumeCapacity.Should().BeNull();
        dto.WeightOccupancy.Should().Be(0m);
        dto.VolumeOccupancy.Should().Be(0m);
    }
}
