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

public class CancelStockOutCommandHandlerTests : IntegrationTestBase
{
    public CancelStockOutCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task From_draft_releases_reservation_and_emits_no_return_event()
    {
        // Arrange: OnHand=10, Reserved=4, Draft stock-out for 4.
        var product = TestData.Product("CAN-1");
        var location = TestData.Location("CAN-LOC-1");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(4));

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(4));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);

        var handler = new CancelStockOutCommandHandler(actContext);

        // Act
        var result = await handler.Handle(
            new CancelStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloadedStockOut = await verify.StockOuts
            .AsNoTracking()
            .SingleAsync(s => s.Id == stockOut.Id, TestContext.Current.CancellationToken);
        reloadedStockOut.Status.Should().Be(StockOutStatus.Cancelled);

        var reloadedInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.Id == inv.Id, TestContext.Current.CancellationToken);
        reloadedInv.OnHand.Value.Should().Be(10);
        reloadedInv.Reserved.Value.Should().Be(0);

        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockOut.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        movements.Should().BeEmpty();

        EventDispatcher.DispatchedEvents
            .OfType<StockOutItemReturnedToStockDomainEvent>()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task From_picking_releases_reservation_and_emits_no_return_event()
    {
        // Picking now means reservation only — no physical removal — so a
        // cancel from Picking behaves like a cancel from Draft.
        var product = TestData.Product("CAN-2");
        var location = TestData.Location("CAN-LOC-2");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(4));

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(4));
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);

        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new CancelStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var reloadedInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(10);
        reloadedInv.Reserved.Value.Should().Be(0);

        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockOut.Id)
            .ToListAsync(ct);
        movements.Should().BeEmpty();

        EventDispatcher.DispatchedEvents
            .OfType<StockOutItemReturnedToStockDomainEvent>()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task From_packed_returns_physical_stock_and_emits_return_event_with_movement()
    {
        // Arrange: inventory in post-pack state (OnHand reduced, Reserved=0).
        var product = TestData.Product("CAN-3");
        var location = TestData.Location("CAN-LOC-3");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(4));
        inv.Pick(new Quantity(4)); // simulates PackStockOut having run

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(4));
        stockOut.StartPicking();
        stockOut.Pack();
        stockOut.ClearDomainEvents(); // pretend the picked event already shipped

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);

        var handler = new CancelStockOutCommandHandler(actContext);

        // Act
        var result = await handler.Handle(
            new CancelStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.Id == inv.Id, TestContext.Current.CancellationToken);
        // OnHand back to 10; Reserved still 0.
        reloadedInv.OnHand.Value.Should().Be(10);
        reloadedInv.Reserved.Value.Should().Be(0);

        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockOut.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        movements.Should().ContainSingle().Which.Should().Match<StockMovement>(m =>
            m.Type == StockMovementType.In
            && m.Source == StockMovementSource.StockOutCancellation
            && m.QuantityChange == 4
            && m.ProductId == product.Id
            && m.LocationId == location.Id);

        EventDispatcher.DispatchedEvents
            .OfType<StockOutItemReturnedToStockDomainEvent>()
            .Should().ContainSingle()
            .Which.Quantity.Should().Be(4);
    }

    [Fact]
    public async Task From_packed_creates_inventory_row_if_one_does_not_exist()
    {
        // Edge case: the only inventory row was deleted between Pack and
        // cancel. Handler should create a new row and dump stock there.
        var product = TestData.Product("CAN-4");
        var location = TestData.Location("CAN-LOC-4");

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(3));
        stockOut.StartPicking();
        stockOut.Pack();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);

        var handler = new CancelStockOutCommandHandler(actContext);
        var result = await handler.Handle(
            new CancelStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var inv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.ProductId == product.Id, TestContext.Current.CancellationToken);
        inv.OnHand.Value.Should().Be(3);
    }

    [Fact]
    public async Task From_shipped_is_rejected()
    {
        var product = TestData.Product("CAN-5");
        var location = TestData.Location("CAN-LOC-5");

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(1));
        stockOut.StartPicking();
        stockOut.Pack();
        stockOut.Ship();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new CancelStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
    }

    [Fact]
    public async Task Missing_stock_out_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new CancelStockOutCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.NotFound");
    }

    private void WireReturnedToStockHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockOutItemReturnedToStockDomainEvent>((evt, ct) =>
            new StockOutItemReturnedToStockDomainEventHandler(context).Handle(evt, ct));
    }
}
