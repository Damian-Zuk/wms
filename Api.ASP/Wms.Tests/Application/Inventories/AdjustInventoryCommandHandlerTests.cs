using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Interfaces;
using Wms.Application.Features.Inventories.Commands;
using Wms.Application.Features.StockMovements.EventHandlers;
using Wms.Domain.Enums;
using Wms.Domain.Events;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Inventories;

public class AdjustInventoryCommandHandlerTests : IntegrationTestBase
{
    public AdjustInventoryCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Positive_adjustment_creates_an_adjustment_in_movement_via_event()
    {
        var product = TestData.Product("ADJ-1");
        var location = TestData.Location("ADJ-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 5);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireAdjustmentHandler(actContext);

        var handler = new AdjustInventoryCommandHandler(actContext);
        var result = await handler.Handle(
            new AdjustInventoryCommand(inv.Id, QuantityChange: 7, Reason: "found stock"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloaded = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.Id == inv.Id, TestContext.Current.CancellationToken);
        reloaded.OnHand.Value.Should().Be(12);

        var movement = await verify.StockMovements
            .AsNoTracking()
            .SingleAsync(m => m.SourceId == inv.Id, TestContext.Current.CancellationToken);

        movement.Type.Should().Be(StockMovementType.In);
        movement.Source.Should().Be(StockMovementSource.Adjustment);
        movement.QuantityChange.Should().Be(7);
        movement.ProductId.Should().Be(product.Id);
        movement.LocationId.Should().Be(location.Id);
    }

    [Fact]
    public async Task Negative_adjustment_produces_an_out_movement_with_absolute_quantity()
    {
        var product = TestData.Product("ADJ-2");
        var location = TestData.Location("ADJ-LOC-2");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireAdjustmentHandler(actContext);

        var handler = new AdjustInventoryCommandHandler(actContext);
        var result = await handler.Handle(
            new AdjustInventoryCommand(inv.Id, QuantityChange: -3, Reason: "shrinkage"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var movement = await verify.StockMovements
            .AsNoTracking()
            .SingleAsync(m => m.SourceId == inv.Id, TestContext.Current.CancellationToken);

        movement.Type.Should().Be(StockMovementType.Out);
        movement.Source.Should().Be(StockMovementSource.Adjustment);
        movement.QuantityChange.Should().Be(3);
    }

    [Fact]
    public async Task Missing_inventory_returns_not_found()
    {
        await using var actContext = CreateContext();
        WireAdjustmentHandler(actContext);

        var handler = new AdjustInventoryCommandHandler(actContext);

        var result = await handler.Handle(
            new AdjustInventoryCommand(Guid.NewGuid(), 1, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.NotFound");
    }

    [Fact]
    public async Task Negative_adjustment_that_breaches_reservation_returns_error_and_no_movement_written()
    {
        var product = TestData.Product("ADJ-3");
        var location = TestData.Location("ADJ-LOC-3");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inv.Reserve(new Wms.Domain.ValueObjects.Quantity(8));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WireAdjustmentHandler(actContext);

        var handler = new AdjustInventoryCommandHandler(actContext);
        var result = await handler.Handle(
            new AdjustInventoryCommand(inv.Id, -5, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.AdjustmentWouldViolateReservation");

        await using var verify = CreateContext();
        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == inv.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        movements.Should().BeEmpty();

        EventDispatcher.DispatchedEvents
            .OfType<InventoryAdjustedDomainEvent>()
            .Should().BeEmpty();
    }

    private void WireAdjustmentHandler(IAppDbContext context)
    {
        EventDispatcher.Register<InventoryAdjustedDomainEvent>((evt, ct) =>
            new InventoryAdjustedDomainEventHandler(context).Handle(evt, ct));
    }
}
