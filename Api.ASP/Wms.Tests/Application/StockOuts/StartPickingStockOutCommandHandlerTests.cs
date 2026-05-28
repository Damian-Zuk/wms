using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.StockOuts.Commands;
using Wms.Domain.Entities;
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
        // Arrange: a Draft stock-out with the reservation already in place
        // (the state CreateStockOut leaves the system in).
        var product = TestData.Product("PICK-1");
        var location = TestData.Location("PICK-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 20);
        inv.Reserve(new Quantity(12));

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(5));
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(7));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new StartPickingStockOutCommandHandler(actContext);

        // Act
        var result = await handler.Handle(
            new StartPickingStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var reloaded = await verify.StockOuts
            .AsNoTracking()
            .SingleAsync(s => s.Id == stockOut.Id, ct);
        reloaded.Status.Should().Be(StockOutStatus.Picking);

        // Inventory is unchanged — OnHand and Reserved are exactly what
        // CreateStockOut left behind.
        var reloadedInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(20);
        reloadedInv.Reserved.Value.Should().Be(12);

        // No StockMovement rows should be created at this stage.
        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockOut.Id)
            .ToListAsync(ct);
        movements.Should().BeEmpty();
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
        var product = TestData.Product("PICK-2");
        var location = TestData.Location("PICK-LOC-2");
        var inv = TestData.Inventory(product.Id, location.Id, null, onHand: 10);
        inv.Reserve(new Quantity(3));

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(3));
        stockOut.StartPicking();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new StartPickingStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new StartPickingStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
    }
}
