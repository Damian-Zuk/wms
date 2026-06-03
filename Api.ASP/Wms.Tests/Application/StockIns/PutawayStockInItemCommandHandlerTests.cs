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

public class PutawayStockInItemCommandHandlerTests : IntegrationTestBase
{
    public PutawayStockInItemCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Partial_putaway_books_stock_reduces_reservation_and_fires_movement()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PUT-1");
        var location = TestData.Location("PUT-1-LOC", capacity: 100);
        var lot = TestData.Lot(product.Id, "PUT-1-LOT", new DateOnly(2027, 01, 01));
        var (stockIn, item) = await ArrangePutawayAsync(product, location, 20, lot);

        await using var actContext = CreateContext();
        WireStockInItemPutawayHandler(actContext);
        var handler = new PutawayStockInItemCommandHandler(actContext);

        var result = await handler.Handle(new PutawayStockInItemCommand(stockIn.Id, item.Id, 8), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedItem = await verify.StockIns
            .AsNoTracking()
            .Where(s => s.Id == stockIn.Id)
            .SelectMany(s => s.Lines)
            .SelectMany(l => l.Items)
            .SingleAsync(i => i.Id == item.Id, ct);
        reloadedItem.PlacedQuantity.Value.Should().Be(8);

        var status = await verify.StockIns.AsNoTracking()
            .Where(s => s.Id == stockIn.Id).Select(s => s.Status).SingleAsync(ct);
        status.Should().Be(StockInStatus.Putaway);

        var inventory = await verify.Inventories.AsNoTracking()
            .SingleAsync(i => i.ProductId == product.Id && i.LocationId == location.Id && i.LotId == lot.Id, ct);
        inventory.OnHand.Value.Should().Be(8);

        var reservation = await verify.CapacityReservations.AsNoTracking()
            .SingleAsync(r => r.StockInItemId == item.Id, ct);
        reservation.Quantity.Value.Should().Be(12);

        var movement = await verify.StockMovements.AsNoTracking()
            .SingleAsync(m => m.SourceId == stockIn.Id, ct);
        movement.Should().Match<StockMovement>(m =>
            m.Type == StockMovementType.In
            && m.Source == StockMovementSource.StockIn
            && m.QuantityChange == 8
            && m.ProductId == product.Id
            && m.LocationId == location.Id
            && m.LotId == lot.Id);

        EventDispatcher.DispatchedEvents
            .OfType<StockInItemPutawayDomainEvent>()
            .Should().ContainSingle()
            .Which.Quantity.Should().Be(8);
    }

    [Fact]
    public async Task Full_putaway_removes_reservation_and_marks_item_fully_placed()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PUT-2");
        var location = TestData.Location("PUT-2-LOC", capacity: 100);
        var (stockIn, item) = await ArrangePutawayAsync(product, location, 15, lot: null);

        await using var actContext = CreateContext();
        WireStockInItemPutawayHandler(actContext);
        var handler = new PutawayStockInItemCommandHandler(actContext);

        var result = await handler.Handle(new PutawayStockInItemCommand(stockIn.Id, item.Id, 15), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var inventory = await verify.Inventories.AsNoTracking()
            .SingleAsync(i => i.ProductId == product.Id && i.LocationId == location.Id, ct);
        inventory.OnHand.Value.Should().Be(15);

        (await verify.CapacityReservations.AnyAsync(r => r.StockInItemId == item.Id, ct))
            .Should().BeFalse();

        var reloadedItem = await verify.StockIns
            .AsNoTracking()
            .Where(s => s.Id == stockIn.Id)
            .SelectMany(s => s.Lines)
            .SelectMany(l => l.Items)
            .SingleAsync(i => i.Id == item.Id, ct);
        reloadedItem.PlacedQuantity.Value.Should().Be(15);
    }

    [Fact]
    public async Task Putaway_increments_existing_inventory_row()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PUT-3");
        var location = TestData.Location("PUT-3-LOC", capacity: 100);
        var (stockIn, item) = await ArrangePutawayAsync(product, location, 7, lot: null);

        Context.Inventories.Add(TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 5));
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new PutawayStockInItemCommandHandler(actContext);

        var result = await handler.Handle(new PutawayStockInItemCommand(stockIn.Id, item.Id, 7), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var inventory = await verify.Inventories.AsNoTracking()
            .SingleAsync(i => i.ProductId == product.Id && i.LocationId == location.Id, ct);
        inventory.OnHand.Value.Should().Be(12);
    }

    [Fact]
    public async Task Rejected_when_quantity_exceeds_remaining()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PUT-4");
        var location = TestData.Location("PUT-4-LOC");
        var (stockIn, item) = await ArrangePutawayAsync(product, location, 20, lot: null);

        await using var actContext = CreateContext();
        var handler = new PutawayStockInItemCommandHandler(actContext);

        var result = await handler.Handle(new PutawayStockInItemCommand(stockIn.Id, item.Id, 25), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.PutawayQuantityExceedsRemaining");

        await using var verify = CreateContext();
        (await verify.Inventories.AnyAsync(i => i.LocationId == location.Id, ct)).Should().BeFalse();
        var reservation = await verify.CapacityReservations.AsNoTracking()
            .SingleAsync(r => r.StockInItemId == item.Id, ct);
        reservation.Quantity.Value.Should().Be(20);
    }

    [Fact]
    public async Task Wrong_status_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PUT-5");
        var location = TestData.Location("PUT-5-LOC");
        // Stays in Draft — putaway is only allowed during Putaway.
        var stockIn = TestData.StockIn(product.Id, location.Id, 10);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var item = stockIn.Lines.Single().Items.Single();

        await using var actContext = CreateContext();
        var handler = new PutawayStockInItemCommandHandler(actContext);

        var result = await handler.Handle(new PutawayStockInItemCommand(stockIn.Id, item.Id, 5), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.CannotPutaway");
    }

    [Fact]
    public async Task Unknown_item_returns_item_not_found()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("PUT-6");
        var location = TestData.Location("PUT-6-LOC");
        var (stockIn, _) = await ArrangePutawayAsync(product, location, 10, lot: null);

        await using var actContext = CreateContext();
        var handler = new PutawayStockInItemCommandHandler(actContext);

        var result = await handler.Handle(new PutawayStockInItemCommand(stockIn.Id, Guid.NewGuid(), 1), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.ItemNotFound");
    }

    [Fact]
    public async Task Missing_stock_in_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new PutawayStockInItemCommandHandler(actContext);

        var result = await handler.Handle(
            new PutawayStockInItemCommand(Guid.NewGuid(), Guid.NewGuid(), 1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.NotFound");
    }

    /// <summary>
    /// Seeds a product/location (and optional lot), a StockIn driven to Putaway, and
    /// the capacity hold that StartPutaway would have created — so a putaway has
    /// something to book against and shrink.
    /// </summary>
    private async Task<(StockIn StockIn, StockInItem Item)> ArrangePutawayAsync(
        Product product, Location location, int quantity, Lot? lot)
    {
        var ct = TestContext.Current.CancellationToken;

        var stockIn = TestData.StockIn(product.Id, location.Id, quantity, lot?.Id);
        stockIn.StartPutaway();
        stockIn.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        if (lot is not null)
            Context.Lots.Add(lot);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var item = stockIn.Lines.Single().Items.Single();
        Context.CapacityReservations.Add(new CapacityReservation(
            stockIn.Id, item.Id, location.Id, product.Id, lot?.Id, new Quantity(quantity)));
        await Context.SaveChangesAsync(ct);

        return (stockIn, item);
    }

    private void WireStockInItemPutawayHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockInItemPutawayDomainEvent>((evt, ct) =>
        {
            var handler = new StockInItemPutawayDomainEventHandler(context);
            return handler.Handle(evt, ct);
        });
    }
}
