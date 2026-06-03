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

public class PackStockOutCommandHandlerTests : IntegrationTestBase
{
    public PackStockOutCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Packing_decrements_inventory_and_creates_out_movements_via_event()
    {
        // Arrange: a Picking stock-out — reservation is in place from
        // CreateStockOut, status was flipped by StartPicking.
        var product = TestData.Product("PACK-1");
        var location = TestData.Location("PACK-LOC");
        var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 20);
        inv.Reserve(new Quantity(12));

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(5));
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(7));
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inv);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        WirePickedHandler(actContext);

        var handler = new PackStockOutCommandHandler(actContext);

        // Act
        var result = await handler.Handle(
            new PackStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var reloaded = await verify.StockOuts
            .AsNoTracking()
            .SingleAsync(s => s.Id == stockOut.Id, ct);
        reloaded.Status.Should().Be(StockOutStatus.Packed);

        var reloadedInv = await verify.Inventories
            .AsNoTracking()
            .SingleAsync(i => i.Id == inv.Id, ct);
        // OnHand 20 - (5+7) = 8; Reserved 12 - (5+7) = 0.
        reloadedInv.OnHand.Value.Should().Be(8);
        reloadedInv.Reserved.Value.Should().Be(0);

        var movements = await verify.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == stockOut.Id)
            .ToListAsync(ct);

        movements.Should().HaveCount(2);
        movements.Should().AllSatisfy(m =>
        {
            m.Type.Should().Be(StockMovementType.Out);
            m.Source.Should().Be(StockMovementSource.StockOut);
            m.ProductId.Should().Be(product.Id);
            m.LocationId.Should().Be(location.Id);
        });
        movements.Select(m => m.QuantityChange).Should().BeEquivalentTo(new[] { 5, 7 });
    }

    [Fact]
    public async Task Missing_stock_out_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new PackStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new PackStockOutCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.NotFound");
    }

    [Fact]
    public async Task Stock_out_not_in_picking_is_rejected()
    {
        // Draft → Pack must fail (wrong status).
        var product = TestData.Product("PACK-2");
        var location = TestData.Location("PACK-LOC-2");

        var inventory = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 10);
        inventory.Reserve(new Quantity(1));

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(1));

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inventory);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new PackStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new PackStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
    }

    [Fact]
    public async Task Missing_inventory_row_for_an_item_returns_insufficient_inventory()
    {
        // No inventory row exists for the (product, location) pair the
        // stock-out points at — Pack cannot proceed.
        var product = TestData.Product("PACK-3");
        var location = TestData.Location("PACK-LOC-3");

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(1));
        stockOut.StartPicking();
        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var actContext = CreateContext();
        var handler = new PackStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new PackStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InsufficientInventory");
    }

    private void WirePickedHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockOutItemPickedDomainEvent>((evt, ct) =>
            new StockOutItemPickedDomainEventHandler(context).Handle(evt, ct));
    }
}
