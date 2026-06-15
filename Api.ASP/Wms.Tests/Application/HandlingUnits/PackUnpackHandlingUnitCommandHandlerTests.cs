using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.HandlingUnits.Commands;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.HandlingUnits;

public class PackUnpackHandlingUnitCommandHandlerTests : IntegrationTestBase
{
    public PackUnpackHandlingUnitCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Pack_moves_loose_stock_onto_the_unit_without_movements()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-PK-1");
        var location = TestData.Location("HU-PK-1-LOC");
        var handlingUnit = TestData.HandlingUnit("HU-PK-1-PAL", locationId: location.Id);
        var receivedAt = new DateTime(2026, 01, 15, 0, 0, 0, DateTimeKind.Utc);
        var looseRow = TestData.Inventory(product.Id, location.Id, onHand: 10, receivedAt: receivedAt);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(looseRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new PackHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new PackHandlingUnitCommand(handlingUnit.Id, product.Id, null, 4), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var rows = await verify.Inventories.AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(ct);
        rows.Single(r => r.HandlingUnitId == null).OnHand.Value.Should().Be(6);

        var unitRow = rows.Single(r => r.HandlingUnitId == handlingUnit.Id);
        unitRow.OnHand.Value.Should().Be(4);
        unitRow.ReceivedAt.Should().Be(receivedAt, "the packed stock keeps its age");

        (await verify.StockMovements.AnyAsync(ct)).Should().BeFalse("re-bucketing is not a movement");
    }

    [Fact]
    public async Task Pack_cannot_exceed_available_loose_stock()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-PK-2");
        var location = TestData.Location("HU-PK-2-LOC");
        var handlingUnit = TestData.HandlingUnit("HU-PK-2-PAL", locationId: location.Id);
        var looseRow = TestData.Inventory(product.Id, location.Id, onHand: 10);
        looseRow.Reserve(new Quantity(8));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(looseRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new PackHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new PackHandlingUnitCommand(handlingUnit.Id, product.Id, null, 3), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.InsufficientLooseStock");
    }

    [Fact]
    public async Task Pack_onto_an_unplaced_unit_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-PK-3");
        var handlingUnit = TestData.HandlingUnit("HU-PK-3-PAL");
        Context.Products.Add(product);
        Context.HandlingUnits.Add(handlingUnit);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new PackHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new PackHandlingUnitCommand(handlingUnit.Id, product.Id, null, 1), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.NotPlaced");
    }

    [Fact]
    public async Task Unpack_moves_unit_stock_back_to_loose()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-UP-1");
        var location = TestData.Location("HU-UP-1-LOC");
        var handlingUnit = TestData.HandlingUnit("HU-UP-1-PAL", locationId: location.Id);
        var unitRow = TestData.Inventory(product.Id, location.Id, onHand: 10, handlingUnitId: handlingUnit.Id);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(unitRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new UnpackHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new UnpackHandlingUnitCommand(handlingUnit.Id, product.Id, null, 7), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var rows = await verify.Inventories.AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(ct);
        rows.Single(r => r.HandlingUnitId == handlingUnit.Id).OnHand.Value.Should().Be(3);
        rows.Single(r => r.HandlingUnitId == null).OnHand.Value.Should().Be(7);
        (await verify.StockMovements.AnyAsync(ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Unpack_cannot_exceed_available_unit_stock()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-UP-2");
        var location = TestData.Location("HU-UP-2-LOC");
        var handlingUnit = TestData.HandlingUnit("HU-UP-2-PAL", locationId: location.Id);
        var unitRow = TestData.Inventory(product.Id, location.Id, onHand: 5, handlingUnitId: handlingUnit.Id);
        unitRow.Reserve(new Quantity(4));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(unitRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new UnpackHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(
            new UnpackHandlingUnitCommand(handlingUnit.Id, product.Id, null, 2), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");
    }
}
