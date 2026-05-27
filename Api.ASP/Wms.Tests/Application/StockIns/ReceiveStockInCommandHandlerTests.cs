using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.StockIns.Commands;
using Wms.Application.Features.StockMovements.EventHandlers;
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
        // Arrange: product/location/lot and a StockIn that is already in
        // Receiving status (the only legal source state for Receive()).
        var product = TestData.Product("RCV-1");
        var location = TestData.Location("RCV-LOC");
        var lot = TestData.Lot(product.Id, "RCV-LOT", new DateOnly(2027, 01, 01));

        var stockIn = new StockIn(Guid.NewGuid());
        stockIn.AddItem(product.Id, location.Id, lot.Id, new Quantity(20));
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Lots.Add(lot);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Wire the real StockMovement event handler through the test
        // dispatcher so a Receive → event → StockMovement chain ends up
        // hitting the database.
        await using var actContext = CreateContext();
        WireStockInItemReceivedHandler(actContext);

        var handler = new ReceiveStockInCommandHandler(actContext);

        // Act
        var result = await handler.Handle(
            new ReceiveStockInCommand(stockIn.Id),
            TestContext.Current.CancellationToken);

        // Assert: result, status, inventory, movement.
        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var ct = TestContext.Current.CancellationToken;

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

        // The captured event mirrors what the domain raised.
        EventDispatcher.DispatchedEvents
            .OfType<StockInItemReceivedDomainEvent>()
            .Should().ContainSingle()
            .Which.Quantity.Should().Be(20);
    }

    [Fact]
    public async Task Receiving_increments_existing_inventory_row_when_one_already_exists()
    {
        var product = TestData.Product("RCV-2");
        var location = TestData.Location("RCV-LOC-2");
        var preExisting = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 5);

        var stockIn = new StockIn(Guid.NewGuid());
        stockIn.AddItem(product.Id, location.Id, lotId: null, new Quantity(7));
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(preExisting);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireStockInItemReceivedHandler(actContext);

        var handler = new ReceiveStockInCommandHandler(actContext);
        var result = await handler.Handle(
            new ReceiveStockInCommand(stockIn.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        var ct = TestContext.Current.CancellationToken;

        await using var verify = CreateContext();
        var inventory = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.ProductId == product.Id && i.LocationId == location.Id, ct);
        inventory.OnHand.Value.Should().Be(12);
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
