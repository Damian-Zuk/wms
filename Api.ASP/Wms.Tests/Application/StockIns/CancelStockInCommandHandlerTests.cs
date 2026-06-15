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

public class CancelStockInCommandHandlerTests : IntegrationTestBase
{
    public CancelStockInCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Cancel_from_putaway_releases_remaining_reservations_and_records_phase()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-1");
        var location = TestData.Location("CN-1-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 10);
        stockIn.StartPutaway();

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
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloadedStockIn = await verify.StockIns.AsNoTracking().SingleAsync(s => s.Id == stockIn.Id, ct);
        reloadedStockIn.Status.Should().Be(StockInStatus.Cancelled);
        reloadedStockIn.CancelledFrom.Should().Be(StockInStatus.Putaway);

        (await verify.CapacityReservations.AnyAsync(r => r.StockInId == stockIn.Id, ct))
            .Should().BeFalse();

        // Nothing was put away, so there is nothing to remove and no movement.
        (await verify.StockMovements.AsNoTracking().Where(m => m.SourceId == stockIn.Id).ToListAsync(ct))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_from_putaway_removes_placed_units_and_writes_movement()
    {
        var ct = TestContext.Current.CancellationToken;

        // Inventory as a completed putaway leaves it: 6 units placed and on hand.
        var product = TestData.Product("CN-PUT-1");
        var location = TestData.Location("CN-PUT-1-LOC", capacity: 100);
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 6);

        var stockIn = TestData.StockIn(product.Id, location.Id, 6);
        stockIn.StartPutaway();
        var item = stockIn.Lines.Single().Items.Single();
        stockIn.PutawayItem(item.Id, new Quantity(6));
        stockIn.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireRemovedFromStockHandler(actContext);
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedStockIn = await verify.StockIns.AsNoTracking().SingleAsync(s => s.Id == stockIn.Id, ct);
        reloadedStockIn.Status.Should().Be(StockInStatus.Cancelled);
        reloadedStockIn.CancelledFrom.Should().Be(StockInStatus.Putaway);

        // The 6 placed units are pulled back out of inventory.
        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(0);

        var movements = await verify.StockMovements.AsNoTracking()
            .Where(m => m.SourceId == stockIn.Id).ToListAsync(ct);
        movements.Should().ContainSingle().Which.Should().Match<StockMovement>(m =>
            m.Type == StockMovementType.Out
            && m.Source == StockMovementSource.StockInCancellation
            && m.QuantityChange == 6
            && m.ProductId == product.Id
            && m.LocationId == location.Id);

        EventDispatcher.DispatchedEvents
            .OfType<StockInItemRemovedFromStockDomainEvent>()
            .Should().ContainSingle()
            .Which.Quantity.Should().Be(6);
    }

    [Fact]
    public async Task Cancel_mid_putaway_removes_only_placed_units_and_releases_remaining_hold()
    {
        var ct = TestContext.Current.CancellationToken;

        // Placement of 10: 4 already put away (on hand), 6 still held by capacity.
        var product = TestData.Product("CN-PUT-2");
        var location = TestData.Location("CN-PUT-2-LOC", capacity: 100);
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 4);

        var stockIn = TestData.StockIn(product.Id, location.Id, 10);
        stockIn.StartPutaway();
        var item = stockIn.Lines.Single().Items.Single();
        stockIn.PutawayItem(item.Id, new Quantity(4));
        stockIn.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        // The hold for the 6 not-yet-placed units survives to cancel time.
        var reservation = new CapacityReservation(
            stockIn.Id, item.Id, location.Id, product.Id, null, new Quantity(6));
        Context.CapacityReservations.Add(reservation);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireRemovedFromStockHandler(actContext);
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        // Only the 4 placed units come out; the held remainder never reached inventory.
        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(0);

        (await verify.CapacityReservations.AnyAsync(r => r.StockInId == stockIn.Id, ct))
            .Should().BeFalse();

        var movements = await verify.StockMovements.AsNoTracking()
            .Where(m => m.SourceId == stockIn.Id).ToListAsync(ct);
        movements.Should().ContainSingle().Which.QuantityChange.Should().Be(4);
    }

    [Fact]
    public async Task Cancel_from_draft_succeeds_with_no_reservations()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-2");
        var location = TestData.Location("CN-2-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 10);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloaded = await verify.StockIns.AsNoTracking().SingleAsync(s => s.Id == stockIn.Id, ct);
        reloaded.Status.Should().Be(StockInStatus.Cancelled);
        reloaded.CancelledFrom.Should().Be(StockInStatus.Draft);
    }

    [Fact]
    public async Task Cancel_after_completed_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-3");
        var location = TestData.Location("CN-3-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 10);
        stockIn.StartPutaway();
        var item = stockIn.Lines.Single().Items.Single();
        stockIn.PutawayItem(item.Id, item.Quantity);
        stockIn.Complete();
        stockIn.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
    }

    [Fact]
    public async Task Cancel_soft_deletes_emptied_and_never_placed_declared_units()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-HU-1");
        var location = TestData.Location("CN-HU-1-LOC", capacity: 100);

        // Placed pallet: its 6 units are on hand, keyed to the unit.
        var placedUnit = TestData.HandlingUnit("CN-HU-1-PLACED", locationId: location.Id);
        var inv = TestData.Inventory(product.Id, location.Id, onHand: 6, handlingUnitId: placedUnit.Id);

        // Declared-but-never-placed pallet (still unplaced, no inventory).
        var unplacedUnit = TestData.HandlingUnit("CN-HU-1-UNPLACED");

        var stockIn = new StockIn(Guid.NewGuid());
        stockIn.AddLineWithPlacements(product.Id, null, new Quantity(10),
        [
            new(location.Id, 6, PutawayStrategyType.NearestEmpty, placedUnit.Id),
            new(location.Id, 4, PutawayStrategyType.NearestEmpty, unplacedUnit.Id)
        ]);
        stockIn.StartPutaway();
        var placedItem = stockIn.Lines.Single().Items.Single(i => i.HandlingUnitId == placedUnit.Id);
        stockIn.PutawayItem(placedItem.Id, new Quantity(6));
        stockIn.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.AddRange(placedUnit, unplacedUnit);
        Context.Inventories.Add(inv);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireRemovedFromStockHandler(actContext);
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedInv = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == inv.Id, ct);
        reloadedInv.OnHand.Value.Should().Be(0);

        // Both units ended up holding nothing — soft-deleted (invisible through the filter).
        (await verify.HandlingUnits.AnyAsync(ct)).Should().BeFalse();
        (await verify.HandlingUnits.IgnoreQueryFilters().CountAsync(h => h.IsDeleted, ct)).Should().Be(2);

        var movement = await verify.StockMovements.AsNoTracking().SingleAsync(m => m.SourceId == stockIn.Id, ct);
        movement.HandlingUnitId.Should().Be(placedUnit.Id);
    }

    [Fact]
    public async Task Cancel_keeps_a_declared_unit_that_gained_other_stock()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-HU-2");
        var otherProduct = TestData.Product("CN-HU-2-OTHER");
        var location = TestData.Location("CN-HU-2-LOC", capacity: 100);

        var handlingUnit = TestData.HandlingUnit("CN-HU-2-PAL", locationId: location.Id);
        var stockInRow = TestData.Inventory(product.Id, location.Id, onHand: 6, handlingUnitId: handlingUnit.Id);
        // Someone packed another product onto the same pallet after putaway.
        var packedRow = TestData.Inventory(otherProduct.Id, location.Id, onHand: 3, handlingUnitId: handlingUnit.Id);

        var stockIn = TestData.StockIn(product.Id, location.Id, 6, handlingUnitId: handlingUnit.Id);
        stockIn.StartPutaway();
        var item = stockIn.Lines.Single().Items.Single();
        stockIn.PutawayItem(item.Id, new Quantity(6));
        stockIn.ClearDomainEvents();

        Context.Products.AddRange(product, otherProduct);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.AddRange(stockInRow, packedRow);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireRemovedFromStockHandler(actContext);
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        // The stock-in's own units are reversed…
        (await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == stockInRow.Id, ct))
            .OnHand.Value.Should().Be(0);

        // …but the pallet still carries the other product, so it survives.
        (await verify.HandlingUnits.AnyAsync(h => h.Id == handlingUnit.Id, ct)).Should().BeTrue();
        (await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == packedRow.Id, ct))
            .OnHand.Value.Should().Be(3);
    }

    private void WireRemovedFromStockHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockInItemRemovedFromStockDomainEvent>((evt, ct) =>
            new StockInItemRemovedFromStockDomainEventHandler(context).Handle(evt, ct));
    }
}
