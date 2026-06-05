using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Handlers.StockMovements.Events;
using Wms.Application.Handlers.StockOuts.Commands;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;

public class PickStockOutItemCommandHandlerTests : IntegrationTestBase
{
    public PickStockOutItemCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Full_pick_removes_units_and_creates_out_movement_via_event()
    {
        var ct = TestContext.Current.CancellationToken;

        // A Picking stock-out: reservation is in place from CreateStockOut,
        // status was flipped by StartPicking.
        var product = TestData.Product("PICK-1");
        var location = TestData.Location("PICK-1-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 20);
        inv.Reserve(new Quantity(5));

        var stockOut = TestData.StockOut(product.Id, location.Id, 5);
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var item = stockOut.Lines.Single().Items.Single();

        await using var actContext = CreateContext();
        WirePickedHandler(actContext);
        var handler = new PickStockOutItemCommandHandler(actContext);

        var result = await handler.Handle(new PickStockOutItemCommand(stockOut.Id, item.Id, 5), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        // Status stays Picking — picking an item does not complete the stock-out.
        var reloaded = await verify.StockOuts.AsNoTracking().SingleAsync(s => s.Id == stockOut.Id, ct);
        reloaded.Status.Should().Be(StockOutStatus.Picking);

        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        // OnHand 20 - 5 = 15; Reserved 5 - 5 = 0.
        reloadedInv.OnHand.Value.Should().Be(15);
        reloadedInv.Reserved.Value.Should().Be(0);

        var movement = await verify.StockMovements.AsNoTracking()
            .SingleAsync(m => m.SourceId == stockOut.Id, ct);
        movement.Should().Match<StockMovement>(m =>
            m.Type == StockMovementType.Out
            && m.Source == StockMovementSource.StockOut
            && m.QuantityChange == 5
            && m.ProductId == product.Id
            && m.LocationId == location.Id);

        EventDispatcher.DispatchedEvents
            .OfType<StockOutItemPickedDomainEvent>()
            .Should().ContainSingle()
            .Which.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Partial_pick_books_units_and_leaves_remainder_reserved()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PICK-2");
        var location = TestData.Location("PICK-2-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 20);
        inv.Reserve(new Quantity(5));

        var stockOut = TestData.StockOut(product.Id, location.Id, 5);
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var item = stockOut.Lines.Single().Items.Single();

        await using var actContext = CreateContext();
        WirePickedHandler(actContext);
        var handler = new PickStockOutItemCommandHandler(actContext);

        var result = await handler.Handle(new PickStockOutItemCommand(stockOut.Id, item.Id, 2), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        // OnHand 20 - 2 = 18; Reserved 5 - 2 = 3.
        reloadedInv.OnHand.Value.Should().Be(18);
        reloadedInv.Reserved.Value.Should().Be(3);

        var reloadedItem = await verify.StockOuts
            .AsNoTracking()
            .Where(s => s.Id == stockOut.Id)
            .SelectMany(s => s.Lines)
            .SelectMany(l => l.Items)
            .SingleAsync(i => i.Id == item.Id, ct);
        reloadedItem.PickedQuantity.Value.Should().Be(2);
        reloadedItem.Remaining.Should().Be(3);

        var movement = await verify.StockMovements.AsNoTracking()
            .SingleAsync(m => m.SourceId == stockOut.Id, ct);
        movement.QuantityChange.Should().Be(2);
    }

    [Fact]
    public async Task Missing_stock_out_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new PickStockOutItemCommandHandler(actContext);

        var result = await handler.Handle(
            new PickStockOutItemCommand(Guid.NewGuid(), Guid.NewGuid(), 1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.NotFound");
    }

    [Fact]
    public async Task Unknown_item_returns_item_not_found()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PICK-3");
        var location = TestData.Location("PICK-3-LOC");
        var stockOut = TestData.StockOut(product.Id, location.Id, 1);
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new PickStockOutItemCommandHandler(actContext);

        var result = await handler.Handle(
            new PickStockOutItemCommand(stockOut.Id, Guid.NewGuid(), 1),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.ItemNotFound");
    }

    [Fact]
    public async Task Missing_inventory_row_returns_insufficient_inventory()
    {
        var ct = TestContext.Current.CancellationToken;

        // The stock-out points at a (product, location) that has no inventory row.
        var product = TestData.Product("PICK-4");
        var location = TestData.Location("PICK-4-LOC");
        var stockOut = TestData.StockOut(product.Id, location.Id, 1);
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var item = stockOut.Lines.Single().Items.Single();

        await using var actContext = CreateContext();
        var handler = new PickStockOutItemCommandHandler(actContext);

        var result = await handler.Handle(new PickStockOutItemCommand(stockOut.Id, item.Id, 1), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InsufficientInventory");
    }

    [Fact]
    public async Task Stock_out_not_in_picking_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        // Draft stock-out (StartPicking never called) — picking is rejected.
        var product = TestData.Product("PICK-5");
        var location = TestData.Location("PICK-5-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(1));

        var stockOut = TestData.StockOut(product.Id, location.Id, 1);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var item = stockOut.Lines.Single().Items.Single();

        await using var actContext = CreateContext();
        var handler = new PickStockOutItemCommandHandler(actContext);

        var result = await handler.Handle(new PickStockOutItemCommand(stockOut.Id, item.Id, 1), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.CannotPick");
    }

    private void WirePickedHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockOutItemPickedDomainEvent>((evt, ct) =>
            new StockOutItemPickedDomainEventHandler(context).Handle(evt, ct));
    }
}
