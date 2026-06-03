using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Handlers.StockIns.Commands;
using Wms.Application.Handlers.StockMovements.Events;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockIns;

public class ReceiveStockInCommandHandlerTests : IntegrationTestBase
{
    public ReceiveStockInCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Receiving_creates_inventory_and_fires_stock_movement_via_event()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("RCV-1");
        var location = TestData.Location("RCV-LOC");
        var lot = TestData.Lot(product.Id, "RCV-LOT", new DateOnly(2027, 01, 01));

        var stockIn = TestData.StockIn(product.Id, location.Id, 20, lot.Id);
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Lots.Add(lot);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        // Wire the real StockMovement event handler through the test dispatcher so a
        // Receive → event → StockMovement chain ends up hitting the database.
        await using var actContext = CreateContext();
        WireStockInItemReceivedHandler(actContext);

        var handler = new ReceiveStockInCommandHandler(actContext);

        var result = await handler.Handle(new ReceiveStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloaded = await verify.StockIns
            .AsNoTracking()
            .SingleAsync(s => s.Id == stockIn.Id, ct);
        reloaded.Status.Should().Be(StockInStatus.Received);

        var inventory = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i =>
                i.ProductId == product.Id
                && i.LocationId == location.Id
                && i.LotId == lot.Id,
                ct);
        inventory.OnHand.Value.Should().Be(20);
        inventory.Reserved.Value.Should().Be(0);

        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockIn.Id)
            .ToListAsync(ct);

        movements.Should().ContainSingle().Which.Should().Match<StockMovement>(m =>
            m.Type == StockMovementType.In
            && m.Source == StockMovementSource.StockIn
            && m.QuantityChange == 20
            && m.ProductId == product.Id
            && m.LocationId == location.Id
            && m.LotId == lot.Id);

        EventDispatcher.DispatchedEvents
            .OfType<StockInItemReceivedDomainEvent>()
            .Should().ContainSingle()
            .Which.Quantity.Should().Be(20);
    }

    [Fact]
    public async Task Receiving_increments_existing_inventory_row_when_one_already_exists()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("RCV-2");
        var location = TestData.Location("RCV-LOC-2");
        var preExisting = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 5);

        var stockIn = TestData.StockIn(product.Id, location.Id, 7);
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(preExisting);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireStockInItemReceivedHandler(actContext);

        var handler = new ReceiveStockInCommandHandler(actContext);
        var result = await handler.Handle(new ReceiveStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var inventory = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.ProductId == product.Id && i.LocationId == location.Id, ct);
        inventory.OnHand.Value.Should().Be(12);
    }

    [Fact]
    public async Task Receiving_splits_into_multiple_inventory_rows_for_a_multi_placement_line()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("RCV-SPL");
        var locationA = TestData.Location("RCV-SPL-A", capacity: 100);
        var locationB = TestData.Location("RCV-SPL-B", capacity: 100);

        var stockIn = new StockIn(Guid.NewGuid());
        stockIn.AddLineWithPlacements(
            product.Id,
            null,
            new Quantity(30),
            [new(locationA.Id, 18, PutawayStrategyType.NearestEmpty), new(locationB.Id, 12, PutawayStrategyType.NearestEmpty)]);
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(locationA);
        Context.Locations.Add(locationB);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireStockInItemReceivedHandler(actContext);

        var handler = new ReceiveStockInCommandHandler(actContext);
        var result = await handler.Handle(new ReceiveStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var a = await verify.Inventories.AsNoTracking().SingleAsync(i => i.LocationId == locationA.Id, ct);
        var b = await verify.Inventories.AsNoTracking().SingleAsync(i => i.LocationId == locationB.Id, ct);
        a.OnHand.Value.Should().Be(18);
        b.OnHand.Value.Should().Be(12);

        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockIn.Id)
            .ToListAsync(ct);
        movements.Should().HaveCount(2);
    }

    [Fact]
    public async Task Receiving_deletes_capacity_reservations()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("RCV-RES");
        var location = TestData.Location("RCV-RES-LOC", capacity: 100);

        var stockIn = TestData.StockIn(product.Id, location.Id, 10);
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var item = stockIn.Lines.Single().Items.Single();
        var reservation = new CapacityReservation(
            stockIn.Id, item.Id, location.Id, product.Id, null, new Quantity(10));
        Context.CapacityReservations.Add(reservation);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new ReceiveStockInCommandHandler(actContext);
        var result = await handler.Handle(new ReceiveStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        (await verify.CapacityReservations.AnyAsync(r => r.StockInId == stockIn.Id, ct))
            .Should().BeFalse();
    }

    private void WireStockInItemReceivedHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockInItemReceivedDomainEvent>((evt, ct) =>
        {
            var handler = new StockInItemReceivedDomainEventHandler(context);
            return handler.Handle(evt, ct);
        });
    }
}
