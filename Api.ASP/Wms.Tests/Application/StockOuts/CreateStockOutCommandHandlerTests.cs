using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockOuts.Commands;
using Wms.Application.Picking;
using Wms.Application.Picking.Strategies;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;

public class CreateStockOutCommandHandlerTests : IntegrationTestBase
{
    public CreateStockOutCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private static IPickingPlanner Planner() => new PickingPlanner(
    [
        new FefoAllocationStrategy(),
        new FifoAllocationStrategy()
    ]);

    [Fact]
    public async Task Fefo_allocates_from_earliest_expiring_lot_and_reserves_each_source()
    {
        var ct = TestContext.Current.CancellationToken;

        // One product with two lots in the same location. The line asks for FEFO,
        // so the earliest-expiring lot must be drained before the later one.
        var product = TestData.Product("SO-PROD");
        var location = TestData.Location("SO-LOC");

        var earlyLot = TestData.Lot(product.Id, "EARLY", new DateOnly(2026, 06, 01));
        var lateLot = TestData.Lot(product.Id, "LATE", new DateOnly(2027, 06, 01));

        var earlyInv = TestData.Inventory(product.Id, location.Id, earlyLot.Id, onHand: 8);
        var lateInv = TestData.Inventory(product.Id, location.Id, lateLot.Id, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Lots.AddRange(earlyLot, lateLot);
        Context.Inventories.AddRange(earlyInv, lateInv);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockOutCommandHandler(actContext, Planner());

        // Pull 12 units: 8 from early (drained) + 4 from late.
        var result = await handler.Handle(
            new CreateStockOutCommand([new StockOutLineRequest(product.Id, PickingStrategyType.Fefo, 12)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var stockOut = await verify.StockOuts
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == result.Value, ct);

        stockOut.Status.Should().Be(StockOutStatus.Draft);
        var line = stockOut.Lines.Should().ContainSingle().Subject;
        line.Items.Should().HaveCount(2);

        var qtyByLot = line.Items.ToDictionary(i => i.LotId!.Value, i => i.Quantity.Value);
        qtyByLot[earlyLot.Id].Should().Be(8);
        qtyByLot[lateLot.Id].Should().Be(4);

        var inventories = await verify.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(ct);

        var earlyAfter = inventories.Single(i => i.LotId == earlyLot.Id);
        earlyAfter.OnHand.Value.Should().Be(8);
        earlyAfter.Reserved.Value.Should().Be(8);
        earlyAfter.Available.Value.Should().Be(0);

        var lateAfter = inventories.Single(i => i.LotId == lateLot.Id);
        lateAfter.OnHand.Value.Should().Be(10);
        lateAfter.Reserved.Value.Should().Be(4);
        lateAfter.Available.Value.Should().Be(6);
    }

    [Fact]
    public async Task Fifo_allocates_from_earliest_received_stock_first()
    {
        var ct = TestContext.Current.CancellationToken;

        // Two locations hold the same (lotless) product received on different
        // dates; FIFO must draw from the oldest receipt first.
        var product = TestData.Product("SO-FIFO");
        var oldLoc = TestData.Location("SO-FIFO-OLD");
        var newLoc = TestData.Location("SO-FIFO-NEW");

        var oldInv = TestData.Inventory(product.Id, oldLoc.Id, lotId: null, onHand: 6,
            receivedAt: new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc));
        var newInv = TestData.Inventory(product.Id, newLoc.Id, lotId: null, onHand: 6,
            receivedAt: new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc));

        Context.Products.Add(product);
        Context.Locations.AddRange(oldLoc, newLoc);
        Context.Inventories.AddRange(oldInv, newInv);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockOutCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockOutCommand([new StockOutLineRequest(product.Id, PickingStrategyType.Fifo, 9)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var inventories = await verify.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(ct);

        // Oldest location fully reserved (6); newest covers the remaining 3.
        inventories.Single(i => i.LocationId == oldLoc.Id).Reserved.Value.Should().Be(6);
        inventories.Single(i => i.LocationId == newLoc.Id).Reserved.Value.Should().Be(3);
    }

    [Fact]
    public async Task Non_lot_tracked_product_reserves_the_lotless_row()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("SO-NOLOT");
        var location = TestData.Location("SO-NOLOT-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 8);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockOutCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockOutCommand([new StockOutLineRequest(product.Id, PickingStrategyType.Fefo, 5)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloaded = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        reloaded.OnHand.Value.Should().Be(8);
        reloaded.Reserved.Value.Should().Be(5);

        var stockOut = await verify.StockOuts
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == result.Value, ct);
        stockOut.Lines.Single().Items.Should().ContainSingle()
            .Which.LotId.Should().BeNull();
    }

    [Fact]
    public async Task Missing_product_returns_product_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new CreateStockOutCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockOutCommand([new StockOutLineRequest(Guid.NewGuid(), PickingStrategyType.Fefo, 1)]),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.ProductNotFound");
    }

    [Fact]
    public async Task Cannot_pick_full_quantity_when_no_inventory_exists()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("SO-FAIL-1");
        Context.Products.Add(product);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockOutCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockOutCommand([new StockOutLineRequest(product.Id, PickingStrategyType.Fefo, 1)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Picking.CannotPickFullQuantity");
    }

    [Fact]
    public async Task Insufficient_available_stock_fails_and_persists_nothing()
    {
        var ct = TestContext.Current.CancellationToken;

        // Enough OnHand, but most of it is already reserved, so the planner
        // cannot cover the request out of Available.
        var product = TestData.Product("SO-FAIL-2");
        var location = TestData.Location("SO-FAIL-LOC-2");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 5);
        inv.Reserve(new Quantity(4));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockOutCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockOutCommand([new StockOutLineRequest(product.Id, PickingStrategyType.Fefo, 3)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Picking.CannotPickFullQuantity");

        await using var verify = CreateContext();
        (await verify.StockOuts.AsNoTracking().AnyAsync(ct)).Should().BeFalse();
        var unchanged = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        unchanged.Reserved.Value.Should().Be(4);
    }
}
