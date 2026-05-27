using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.StockTransfers.Commands;
using Wms.Domain.Enums;
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

    [Fact]
    public async Task Lot_transfer_moves_stock_into_a_new_destination_inventory_row()
    {
        var product = TestData.Product("XF-LOT");
        var source = TestData.Location("XF-LOT-SRC");
        var destination = TestData.Location("XF-LOT-DST");
        var lot = TestData.Lot(product.Id, "LOT-1", new DateOnly(2027, 01, 01));
        var sourceInv = TestData.Inventory(product.Id, source.Id, lot.Id, onHand: 6);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.Lots.Add(lot);
        Context.Inventories.Add(sourceInv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new TransferStockCommandHandler(actContext);

        var result = await handler.Handle(
            new TransferStockCommand(product.Id, source.Id, destination.Id, LotId: lot.Id, Quantity: 2),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var destInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.LocationId == destination.Id && i.LotId == lot.Id, ct);
        destInv.OnHand.Value.Should().Be(2);

        var srcInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.LocationId == source.Id && i.LotId == lot.Id, ct);
        srcInv.OnHand.Value.Should().Be(4);
    }

    public class CanAcceptFailures : TransferStockCommandHandlerTests
    {
        public CanAcceptFailures(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Destination_temperature_mismatch_is_rejected()
        {
            // Frozen product, Ambient destination.
            var product = TestData.Product("XF-T", temperatureZone: TemperatureZone.Frozen);
            var source = TestData.Location("XF-T-SRC", temperatureZone: TemperatureZone.Frozen);
            var destination = TestData.Location("XF-T-DST", temperatureZone: TemperatureZone.Ambient);
            var sourceInv = TestData.Inventory(product.Id, source.Id, null, onHand: 5);

            Context.Products.Add(product);
            Context.Locations.AddRange(source, destination);
            Context.Inventories.Add(sourceInv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new TransferStockCommandHandler(actContext);

            var result = await handler.Handle(
                new TransferStockCommand(product.Id, source.Id, destination.Id, null, 1),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.TemperatureMismatch");
        }

        [Fact]
        public async Task Destination_capacity_exceeded_is_rejected()
        {
            var product = TestData.Product("XF-C");
            var source = TestData.Location("XF-C-SRC");
            // Destination already has 8 units; capacity 10; trying to move 5
            // pushes total to 13.
            var destination = TestData.Location("XF-C-DST", capacity: 10);
            var sourceInv = TestData.Inventory(product.Id, source.Id, null, onHand: 5);
            var destInv = TestData.Inventory(product.Id, destination.Id, null, onHand: 8);

            Context.Products.Add(product);
            Context.Locations.AddRange(source, destination);
            Context.Inventories.AddRange(sourceInv, destInv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new TransferStockCommandHandler(actContext);

            var result = await handler.Handle(
                new TransferStockCommand(product.Id, source.Id, destination.Id, null, Quantity: 5),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.CapacityExceeded");
        }

        [Fact]
        public async Task Destination_mixed_sku_rule_is_rejected()
        {
            // Destination already holds a different product and disallows mixing.
            var movingProduct = TestData.Product("XF-MOVE");
            var residentProduct = TestData.Product("XF-RESIDENT");
            var source = TestData.Location("XF-MIX-SRC");
            var destination = TestData.Location("XF-MIX-DST", isMixedSkuAllowed: false);
            var sourceInv = TestData.Inventory(movingProduct.Id, source.Id, null, onHand: 5);
            var residentInv = TestData.Inventory(residentProduct.Id, destination.Id, null, onHand: 3);

            Context.Products.AddRange(movingProduct, residentProduct);
            Context.Locations.AddRange(source, destination);
            Context.Inventories.AddRange(sourceInv, residentInv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new TransferStockCommandHandler(actContext);

            var result = await handler.Handle(
                new TransferStockCommand(movingProduct.Id, source.Id, destination.Id, null, 1),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.MixedSkuNotAllowed");
        }
    }
}
