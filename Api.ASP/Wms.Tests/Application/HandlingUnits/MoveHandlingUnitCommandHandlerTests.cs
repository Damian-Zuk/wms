using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Handlers.HandlingUnits.Commands;
using Wms.Application.Handlers.StockMovements.Events;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.HandlingUnits;

public class MoveHandlingUnitCommandHandlerTests : IntegrationTestBase
{
    public MoveHandlingUnitCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Moves_the_unit_with_all_contents_and_writes_paired_movements()
    {
        var ct = TestContext.Current.CancellationToken;

        var productA = TestData.Product("HU-MV-1A");
        var productB = TestData.Product("HU-MV-1B");
        var source = TestData.Location("HU-MV-1-SRC", capacity: 100);
        var destination = TestData.Location("HU-MV-1-DST", capacity: 100);
        var handlingUnit = TestData.HandlingUnit("HU-MV-1-PAL", locationId: source.Id);

        var receivedAt = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc);
        var rowA = TestData.Inventory(productA.Id, source.Id, onHand: 6,
            receivedAt: receivedAt, handlingUnitId: handlingUnit.Id);
        var rowB = TestData.Inventory(productB.Id, source.Id, onHand: 4, handlingUnitId: handlingUnit.Id);
        // Loose stock at the source stays behind.
        var looseRow = TestData.Inventory(productA.Id, source.Id, onHand: 3);

        Context.Products.AddRange(productA, productB);
        Context.Locations.AddRange(source, destination);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.AddRange(rowA, rowB, looseRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireMovedHandler(actContext);
        var handler = new MoveHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new MoveHandlingUnitCommand(handlingUnit.Id, destination.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloadedUnit = await verify.HandlingUnits.AsNoTracking().SingleAsync(h => h.Id == handlingUnit.Id, ct);
        reloadedUnit.LocationId.Should().Be(destination.Id);

        var reloadedRowA = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == rowA.Id, ct);
        reloadedRowA.LocationId.Should().Be(destination.Id);
        reloadedRowA.ReceivedAt.Should().Be(receivedAt, "FIFO age travels with the pallet");

        var reloadedRowB = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == rowB.Id, ct);
        reloadedRowB.LocationId.Should().Be(destination.Id);

        var reloadedLoose = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == looseRow.Id, ct);
        reloadedLoose.LocationId.Should().Be(source.Id);

        // Two rows moved → two out/in movement pairs sharing one move id.
        var movements = await verify.StockMovements.AsNoTracking()
            .Where(m => m.HandlingUnitId == handlingUnit.Id)
            .ToListAsync(ct);
        movements.Should().HaveCount(4);
        movements.Select(m => m.SourceId).Distinct().Should().HaveCount(1);
        movements.Should().OnlyContain(m => m.Source == StockMovementSource.HandlingUnitMove);
        movements.Count(m => m.Type == StockMovementType.Out && m.LocationId == source.Id).Should().Be(2);
        movements.Count(m => m.Type == StockMovementType.In && m.LocationId == destination.Id).Should().Be(2);
    }

    [Fact]
    public async Task Reserved_stock_anywhere_on_the_unit_blocks_the_move()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-MV-2");
        var source = TestData.Location("HU-MV-2-SRC");
        var destination = TestData.Location("HU-MV-2-DST");
        var handlingUnit = TestData.HandlingUnit("HU-MV-2-PAL", locationId: source.Id);

        var row = TestData.Inventory(product.Id, source.Id, onHand: 10, handlingUnitId: handlingUnit.Id);
        row.Reserve(new Quantity(1));

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(row);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new MoveHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new MoveHandlingUnitCommand(handlingUnit.Id, destination.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.HasReservedStock");

        await using var verify = CreateContext();
        (await verify.HandlingUnits.AsNoTracking().SingleAsync(h => h.Id == handlingUnit.Id, ct))
            .LocationId.Should().Be(source.Id);
    }

    [Fact]
    public async Task Destination_that_cannot_take_the_whole_unit_blocks_the_move()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-MV-3");
        var source = TestData.Location("HU-MV-3-SRC");
        // Destination already holds 95/100; the 10-unit pallet does not fit.
        var destination = TestData.Location("HU-MV-3-DST", capacity: 100);
        var handlingUnit = TestData.HandlingUnit("HU-MV-3-PAL", locationId: source.Id);

        var row = TestData.Inventory(product.Id, source.Id, onHand: 10, handlingUnitId: handlingUnit.Id);
        var occupying = TestData.Inventory(product.Id, destination.Id, onHand: 95);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.AddRange(row, occupying);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new MoveHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new MoveHandlingUnitCommand(handlingUnit.Id, destination.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.CapacityExceeded");
    }

    [Fact]
    public async Task Empty_unit_moves_without_movements()
    {
        var ct = TestContext.Current.CancellationToken;

        var source = TestData.Location("HU-MV-4-SRC");
        var destination = TestData.Location("HU-MV-4-DST");
        var handlingUnit = TestData.HandlingUnit("HU-MV-4-PAL", locationId: source.Id);

        Context.Locations.AddRange(source, destination);
        Context.HandlingUnits.Add(handlingUnit);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireMovedHandler(actContext);
        var handler = new MoveHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new MoveHandlingUnitCommand(handlingUnit.Id, destination.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        (await verify.HandlingUnits.AsNoTracking().SingleAsync(h => h.Id == handlingUnit.Id, ct))
            .LocationId.Should().Be(destination.Id);
        (await verify.StockMovements.AnyAsync(ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Unplaced_unit_cannot_be_moved()
    {
        var ct = TestContext.Current.CancellationToken;

        var destination = TestData.Location("HU-MV-5-DST");
        var handlingUnit = TestData.HandlingUnit("HU-MV-5-PAL");

        Context.Locations.Add(destination);
        Context.HandlingUnits.Add(handlingUnit);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new MoveHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new MoveHandlingUnitCommand(handlingUnit.Id, destination.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.NotPlaced");
    }

    private void WireMovedHandler(IAppDbContext context)
    {
        EventDispatcher.Register<HandlingUnitMovedDomainEvent>((evt, ct) =>
            new HandlingUnitMovedDomainEventHandler(context).Handle(evt, ct));
    }
}
