using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.StockTransfers.Commands;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockTransfers;

public class TransferStockCommandHandlerTests : IntegrationTestBase
{
    public TransferStockCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Happy_path_moves_on_hand_between_locations()
    {
        // Arrange: same product in source (10 units) and an empty destination.
        var product = TestData.Product("XF-P1");
        var source = TestData.Location("XF-SRC");
        var destination = TestData.Location("XF-DST");
        var sourceInv = TestData.Inventory(product.Id, source.Id, lotId: null, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.Inventories.Add(sourceInv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new TransferStockCommandHandler(actContext);

        // Act
        var result = await handler.Handle(
            new TransferStockCommand(product.Id, source.Id, destination.Id, LotId: null, Quantity: 4),
            TestContext.Current.CancellationToken);

        // Assert: succeeded and on-hand moved.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var ct = TestContext.Current.CancellationToken;

        await using var verify = CreateContext();
        var inventories = await verify.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(ct);

        inventories.Single(i => i.LocationId == source.Id).OnHand.Value.Should().Be(6);
        inventories.Single(i => i.LocationId == destination.Id).OnHand.Value.Should().Be(4);
    }

    [Fact]
    public async Task Same_source_and_destination_is_rejected()
    {
        var product = TestData.Product("XF-P2");
        var location = TestData.Location("XF-ONE");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 5);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new TransferStockCommandHandler(actContext);

        var result = await handler.Handle(
            new TransferStockCommand(product.Id, location.Id, location.Id, LotId: null, Quantity: 1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockTransfer.SameSourceAndDestination");

        var ct = TestContext.Current.CancellationToken;

        await using var verify = CreateContext();
        var unchanged = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.LocationId == location.Id, ct);
        unchanged.OnHand.Value.Should().Be(5);
    }

    [Fact]
    public async Task Transfer_exceeding_available_stock_is_rejected()
    {
        var product = TestData.Product("XF-P3");
        var source = TestData.Location("XF-SRC-3");
        var destination = TestData.Location("XF-DST-3");
        var sourceInv = TestData.Inventory(product.Id, source.Id, lotId: null, onHand: 3);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.Inventories.Add(sourceInv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new TransferStockCommandHandler(actContext);

        var result = await handler.Handle(
            new TransferStockCommand(product.Id, source.Id, destination.Id, LotId: null, Quantity: 5),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");
    }
}
