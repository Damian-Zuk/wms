using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.StockMovements.EventHandlers;
using Wms.Application.Features.StockOuts.Commands;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;
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
    public async Task Picking_decrements_inventory_and_creates_out_movements_via_event()
    {
        // Arrange: a Draft stock-out with two items already reserved against
        // inventory (the state CreateStockOut leaves the system in).
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
        WirePickedHandler(actContext);

        var handler = new StartPickingStockOutCommandHandler(actContext);

        // Act
        var result = await handler.Handle(
            new StartPickingStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloaded = await verify.StockOuts
            .AsNoTracking()
            .SingleAsync(s => s.Id == stockOut.Id, TestContext.Current.CancellationToken);
        reloaded.Status.Should().Be(StockOutStatus.Picking);

        var reloadedInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.Id == inv.Id, TestContext.Current.CancellationToken);
        // OnHand 20 - (5+7) = 8; Reserved 12 - (5+7) = 0.
        reloadedInv.OnHand.Value.Should().Be(8);
        reloadedInv.Reserved.Value.Should().Be(0);

        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockOut.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        movements.Should().HaveCount(2);
        movements.Should().AllSatisfy(m =>
        {
            m.Type.Should().Be(StockMovementType.Out);
            m.Source.Should().Be(StockMovementSource.StockOut);
            m.ProductId.Should().Be(product.Id);
            m.LocationId.Should().Be(location.Id);
        });
        movements.Select(m => m.QuantityChange).Should().BeEquivalentTo(new[] { 5, 7 });
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

    [Fact]
    public async Task Missing_inventory_row_for_an_item_returns_insufficient_inventory()
    {
        // The stock-out points at (product, location) for which no
        // Inventory row exists.
        var product = TestData.Product("PICK-3");
        var location = TestData.Location("PICK-LOC-3");

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(1));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new StartPickingStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new StartPickingStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InsufficientInventory");
    }

    private void WirePickedHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockOutItemPickedDomainEvent>((evt, ct) =>
            new StockOutItemPickedDomainEventHandler(context).Handle(evt, ct));
    }
}
