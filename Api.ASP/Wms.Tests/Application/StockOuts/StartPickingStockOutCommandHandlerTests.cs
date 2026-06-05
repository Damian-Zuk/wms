using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockOuts.Commands;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;

public class StartPickingStockOutCommandHandlerTests : IntegrationTestBase
{
    public StartPickingStockOutCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Draft_transitions_to_picking_without_touching_inventory_or_movements()
    {
        var ct = TestContext.Current.CancellationToken;

        // A Draft stock-out with the reservation already in place (the state
        // CreateStockOut leaves the system in).
        var product = TestData.Product("PICK-1");
        var location = TestData.Location("PICK-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 20);
        inv.Reserve(new Quantity(12));

        var stockOut = TestData.StockOut(product.Id, location.Id, 12);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new StartPickingStockOutCommandHandler(actContext);

        var result = await handler.Handle(new StartPickingStockOutCommand(stockOut.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloaded = await verify.StockOuts.AsNoTracking().SingleAsync(s => s.Id == stockOut.Id, ct);
        reloaded.Status.Should().Be(StockOutStatus.Picking);

        // Inventory is exactly what CreateStockOut left behind.
        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(20);
        reloadedInv.Reserved.Value.Should().Be(12);

        (await verify.StockMovements.AsNoTracking().Where(m => m.SourceId == stockOut.Id).ToListAsync(ct))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_stock_out_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new StartPickingStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new StartPickingStockOutCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.NotFound");
    }

    [Fact]
    public async Task Stock_out_not_in_draft_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PICK-2");
        var location = TestData.Location("PICK-LOC-2");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(3));

        var stockOut = TestData.StockOut(product.Id, location.Id, 3);
        stockOut.StartPicking();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new StartPickingStockOutCommandHandler(actContext);

        var result = await handler.Handle(new StartPickingStockOutCommand(stockOut.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
    }
}
