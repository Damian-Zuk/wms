using FluentAssertions;
using Wms.Application.Allocations;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Allocations;

public class FefoAllocatorTests : IntegrationTestBase
{
    public FefoAllocatorTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Allocates_from_earliest_expiring_lot_first()
    {
        // Arrange: one product, one location, three lots with distinct
        // expiry dates spread out so ordering is unambiguous.
        var product = TestData.Product("FEFO-1");
        var location = TestData.Location("FEFO-LOC");

        var lotEarliest = TestData.Lot(product.Id, "LOT-EARLY", new DateOnly(2026, 06, 01));
        var lotMiddle = TestData.Lot(product.Id, "LOT-MID", new DateOnly(2026, 09, 01));
        var lotLatest = TestData.Lot(product.Id, "LOT-LATE", new DateOnly(2026, 12, 01));

        // 10 units per lot.
        var invEarliest = TestData.Inventory(product.Id, location.Id, lotEarliest.Id, onHand: 10);
        var invMiddle = TestData.Inventory(product.Id, location.Id, lotMiddle.Id, onHand: 10);
        var invLatest = TestData.Inventory(product.Id, location.Id, lotLatest.Id, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Lots.AddRange(lotEarliest, lotMiddle, lotLatest);
        Context.Inventories.AddRange(invEarliest, invMiddle, invLatest);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var allocator = new FefoAllocator(Context);

        // Act: request 15 units. Should fully drain the earliest lot
        // (10 units) and take 5 from the next.
        var result = await allocator.AllocateAsync(
            product.Id,
            location.Id,
            new Quantity(15),
            TestContext.Current.CancellationToken);

        // Assert: order matches FEFO and latest lot is untouched.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        result.Value[0].LotId.Should().Be(lotEarliest.Id);
        result.Value[0].Quantity.Value.Should().Be(10);

        result.Value[1].LotId.Should().Be(lotMiddle.Id);
        result.Value[1].Quantity.Value.Should().Be(5);
    }

    [Fact]
    public async Task Lots_without_expiry_sort_last()
    {
        var product = TestData.Product("FEFO-2");
        var location = TestData.Location("FEFO-LOC-2");

        var lotNoExpiry = TestData.Lot(product.Id, "LOT-NO-EXP", expirationDate: null);
        var lotWithExpiry = TestData.Lot(product.Id, "LOT-EXP", new DateOnly(2030, 01, 01));

        var invNoExpiry = TestData.Inventory(product.Id, location.Id, lotNoExpiry.Id, onHand: 5);
        var invWithExpiry = TestData.Inventory(product.Id, location.Id, lotWithExpiry.Id, onHand: 5);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Lots.AddRange(lotNoExpiry, lotWithExpiry);
        Context.Inventories.AddRange(invNoExpiry, invWithExpiry);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var allocator = new FefoAllocator(Context);

        var result = await allocator.AllocateAsync(
            product.Id,
            location.Id,
            new Quantity(7),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].LotId.Should().Be(lotWithExpiry.Id);
        result.Value[1].LotId.Should().Be(lotNoExpiry.Id);
    }

    [Fact]
    public async Task Fails_when_total_available_is_below_requested()
    {
        var product = TestData.Product("FEFO-3");
        var location = TestData.Location("FEFO-LOC-3");
        var lot = TestData.Lot(product.Id, "L1", new DateOnly(2026, 06, 01));
        var inv = TestData.Inventory(product.Id, location.Id, lot.Id, onHand: 3);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Lots.Add(lot);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var allocator = new FefoAllocator(Context);

        var result = await allocator.AllocateAsync(
            product.Id,
            location.Id,
            new Quantity(10),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.InsufficientAvailableStockForFefo");
    }
}
