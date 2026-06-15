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
        var ct = TestContext.Current.CancellationToken;

        // OnHand=10, Reserved=4, Draft stock-out for 4.
        var product = TestData.Product("CAN-1");
        var location = TestData.Location("CAN-LOC-1");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(4));

        var stockOut = TestData.StockOut(product.Id, location.Id, 4);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockOutCommand(stockOut.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloaded = await verify.StockOuts.AsNoTracking().SingleAsync(s => s.Id == stockOut.Id, ct);
        reloaded.Status.Should().Be(StockOutStatus.Cancelled);

        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(10);
        reloadedInv.Reserved.Value.Should().Be(0);

        (await verify.StockMovements.AsNoTracking().Where(m => m.SourceId == stockOut.Id).ToListAsync(ct))
            .Should().BeEmpty();

        EventDispatcher.DispatchedEvents
            .OfType<StockOutItemReturnedToStockDomainEvent>()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task From_picking_with_no_picks_releases_reservation_and_emits_no_return_event()
    {
        var ct = TestContext.Current.CancellationToken;

        // In Picking but nothing has been picked yet — the reserved remainder is
        // simply released, exactly like a cancel from Draft.
        var product = TestData.Product("CAN-2");
        var location = TestData.Location("CAN-LOC-2");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(4));

        var stockOut = TestData.StockOut(product.Id, location.Id, 4);
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockOutCommand(stockOut.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(10);
        reloadedInv.Reserved.Value.Should().Be(0);

        (await verify.StockMovements.AsNoTracking().Where(m => m.SourceId == stockOut.Id).ToListAsync(ct))
            .Should().BeEmpty();

        EventDispatcher.DispatchedEvents
            .OfType<StockOutItemReturnedToStockDomainEvent>()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task From_picking_with_picked_units_returns_them_and_emits_return_event_with_movement()
    {
        var ct = TestContext.Current.CancellationToken;

        // Inventory in mid-pick state: 4 reserved then picked, so OnHand and
        // Reserved both dropped by 4 — mirroring what PickStockOutItem leaves.
        var product = TestData.Product("CAN-3");
        var location = TestData.Location("CAN-LOC-3");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Quantity(4));
        inv.Pick(new Quantity(4));

        var stockOut = TestData.StockOut(product.Id, location.Id, 4);
        stockOut.StartPicking();
        var item = stockOut.Lines.Single().Items.Single();
        stockOut.PickItem(item.Id, new Quantity(4));
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockOutCommand(stockOut.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        // The 4 picked units are returned to stock; nothing remains reserved.
        reloadedInv.OnHand.Value.Should().Be(10);
        reloadedInv.Reserved.Value.Should().Be(0);

        var movements = await verify.StockMovements.AsNoTracking()
            .Where(m => m.SourceId == stockOut.Id).ToListAsync(ct);
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
    public async Task From_picking_creates_inventory_row_if_one_does_not_exist()
    {
        var ct = TestContext.Current.CancellationToken;

        // Edge case: the inventory row the pick drew from is gone by cancel time.
        // The handler recreates a row and dumps the returned units there.
        var product = TestData.Product("CAN-4");
        var location = TestData.Location("CAN-LOC-4");

        var stockOut = TestData.StockOut(product.Id, location.Id, 3);
        stockOut.StartPicking();
        var item = stockOut.Lines.Single().Items.Single();
        stockOut.PickItem(item.Id, new Quantity(3));
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockOutCommand(stockOut.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var inv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.ProductId == product.Id, ct);
        inv.OnHand.Value.Should().Be(3);
    }

    [Fact]
    public async Task From_completed_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CAN-5");
        var location = TestData.Location("CAN-LOC-5");

        var stockOut = TestData.StockOut(product.Id, location.Id, 1);
        stockOut.StartPicking();
        var item = stockOut.Lines.Single().Items.Single();
        stockOut.PickItem(item.Id, new Quantity(1));
        stockOut.Complete();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockOutCommand(stockOut.Id), ct);

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

    [Fact]
    public async Task Cancel_after_pick_returns_stock_to_the_handling_unit_row()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CNO-HU-1");
        var location = TestData.Location("CNO-HU-1-LOC");
        var handlingUnit = TestData.HandlingUnit("CNO-HU-1-PAL", locationId: location.Id);

        // Pallet row picked down to 6 (4 picked, nothing reserved anymore), plus a
        // loose row that must stay untouched by the cancellation.
        var huRow = TestData.Inventory(product.Id, location.Id, onHand: 6, handlingUnitId: handlingUnit.Id);
        var looseRow = TestData.Inventory(product.Id, location.Id, onHand: 9);

        var stockOut = TestData.StockOut(product.Id, location.Id, 4, handlingUnitId: handlingUnit.Id);
        stockOut.StartPicking();
        var item = stockOut.Lines.Single().Items.Single();
        stockOut.PickItem(item.Id, new Quantity(4));
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.AddRange(huRow, looseRow);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireReturnedToStockHandler(actContext);
        var handler = new CancelStockOutCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockOutCommand(stockOut.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedHuRow = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == huRow.Id, ct);
        reloadedHuRow.OnHand.Value.Should().Be(10, "the picked units return to the pallet");

        var reloadedLooseRow = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == looseRow.Id, ct);
        reloadedLooseRow.OnHand.Value.Should().Be(9);

        var movement = await verify.StockMovements.AsNoTracking().SingleAsync(m => m.SourceId == stockOut.Id, ct);
        movement.HandlingUnitId.Should().Be(handlingUnit.Id);
    }

    private void WireReturnedToStockHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockOutItemReturnedToStockDomainEvent>((evt, ct) =>
            new StockOutItemReturnedToStockDomainEventHandler(context).Handle(evt, ct));
    }
}
