using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockOuts.Commands;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;

public class ModifyPickLocationsCommandHandlerTests : IntegrationTestBase
{
    public ModifyPickLocationsCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Re_split_moves_reservations_marks_manual_and_keeps_total()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MP-1");
        var locA = TestData.Location("MP-1-A");
        var locB = TestData.Location("MP-1-B");

        // The draft already reserves all 30 at locA (as CreateStockOut would have).
        var invA = TestData.Inventory(product.Id, locA.Id, lotId: null, onHand: 30);
        invA.Reserve(new Quantity(30));
        var invB = TestData.Inventory(product.Id, locB.Id, lotId: null, onHand: 20);

        var stockOut = TestData.StockOut(product.Id, locA.Id, 30);

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Locations.Add(locB);
        Context.Inventories.Add(invA);
        Context.Inventories.Add(invB);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var lineId = stockOut.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyPickLocationsCommandHandler(actContext);

        var result = await handler.Handle(
            new ModifyPickLocationsCommand(stockOut.Id, lineId,
            [
                new PickAllocationRequest(locA.Id, null, 10),
                new PickAllocationRequest(locB.Id, null, 20),
            ]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var reloaded = await verify.StockOuts
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == stockOut.Id, ct);

        var line = reloaded.Lines.Single();
        line.Items.Should().HaveCount(2);
        line.Items.Sum(i => i.Quantity.Value).Should().Be(30);
        line.Items.Should().OnlyContain(i => i.Strategy == PickingStrategyType.Manual);
        line.Strategy.Should().Be(PickingStrategyType.Manual);

        // Reservation moved: locA dropped from 30 to 10, locB picked up 20.
        var reloadedA = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invA.Id, ct);
        reloadedA.Reserved.Value.Should().Be(10);
        reloadedA.OnHand.Value.Should().Be(30);

        var reloadedB = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invB.Id, ct);
        reloadedB.Reserved.Value.Should().Be(20);
        reloadedB.OnHand.Value.Should().Be(20);
    }

    [Fact]
    public async Task Reallocating_to_a_lot_tracked_source_persists_lot_and_moves_reservation()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MP-2");
        var locA = TestData.Location("MP-2-A");
        var locB = TestData.Location("MP-2-B");
        var lot = TestData.Lot(product.Id, "MP-2-LOT");

        var invA = TestData.Inventory(product.Id, locA.Id, lotId: null, onHand: 10);
        invA.Reserve(new Quantity(10));
        var invB = TestData.Inventory(product.Id, locB.Id, lotId: lot.Id, onHand: 10);

        var stockOut = TestData.StockOut(product.Id, locA.Id, 10);

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Locations.Add(locB);
        Context.Lots.Add(lot);
        Context.Inventories.Add(invA);
        Context.Inventories.Add(invB);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var lineId = stockOut.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyPickLocationsCommandHandler(actContext);

        var result = await handler.Handle(
            new ModifyPickLocationsCommand(stockOut.Id, lineId,
                [new PickAllocationRequest(locB.Id, lot.Id, 10)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var item = await verify.StockOuts
            .AsNoTracking()
            .Where(s => s.Id == stockOut.Id)
            .SelectMany(s => s.Lines)
            .SelectMany(l => l.Items)
            .SingleAsync(ct);
        item.LocationId.Should().Be(locB.Id);
        item.LotId.Should().Be(lot.Id);
        item.Quantity.Value.Should().Be(10);

        var reloadedA = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invA.Id, ct);
        reloadedA.Reserved.Value.Should().Be(0);

        var reloadedB = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invB.Id, ct);
        reloadedB.Reserved.Value.Should().Be(10);
    }

    [Fact]
    public async Task Total_mismatch_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MP-3");
        var locA = TestData.Location("MP-3-A");
        var invA = TestData.Inventory(product.Id, locA.Id, lotId: null, onHand: 30);
        invA.Reserve(new Quantity(30));
        var stockOut = TestData.StockOut(product.Id, locA.Id, 30);

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Inventories.Add(invA);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var lineId = stockOut.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyPickLocationsCommandHandler(actContext);

        var result = await handler.Handle(
            new ModifyPickLocationsCommand(stockOut.Id, lineId,
                [new PickAllocationRequest(locA.Id, null, 25)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.AllocationsDoNotMatchLineTotal");
    }

    [Fact]
    public async Task Target_without_enough_available_is_rejected_and_nothing_changes()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MP-4");
        var locA = TestData.Location("MP-4-A");
        var locB = TestData.Location("MP-4-B");

        var invA = TestData.Inventory(product.Id, locA.Id, lotId: null, onHand: 30);
        invA.Reserve(new Quantity(30));
        // locB only holds 20 — it cannot absorb the full 30.
        var invB = TestData.Inventory(product.Id, locB.Id, lotId: null, onHand: 20);

        var stockOut = TestData.StockOut(product.Id, locA.Id, 30);

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Locations.Add(locB);
        Context.Inventories.Add(invA);
        Context.Inventories.Add(invB);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var lineId = stockOut.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyPickLocationsCommandHandler(actContext);

        var result = await handler.Handle(
            new ModifyPickLocationsCommand(stockOut.Id, lineId,
                [new PickAllocationRequest(locB.Id, null, 30)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");

        await using var verify = CreateContext();

        // The handler never saved: the original reservation and items are untouched.
        var reloadedA = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invA.Id, ct);
        reloadedA.Reserved.Value.Should().Be(30);
        var reloadedB = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invB.Id, ct);
        reloadedB.Reserved.Value.Should().Be(0);

        var item = await verify.StockOuts
            .AsNoTracking()
            .Where(s => s.Id == stockOut.Id)
            .SelectMany(s => s.Lines)
            .SelectMany(l => l.Items)
            .SingleAsync(ct);
        item.LocationId.Should().Be(locA.Id);
        item.Strategy.Should().Be(PickingStrategyType.Fefo);
    }

    [Fact]
    public async Task Rejected_outside_draft_and_reservation_is_untouched()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MP-5");
        var locA = TestData.Location("MP-5-A");
        var locB = TestData.Location("MP-5-B");

        var invA = TestData.Inventory(product.Id, locA.Id, lotId: null, onHand: 30);
        invA.Reserve(new Quantity(30));
        var invB = TestData.Inventory(product.Id, locB.Id, lotId: null, onHand: 30);

        var stockOut = TestData.StockOut(product.Id, locA.Id, 30);
        stockOut.StartPicking();

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Locations.Add(locB);
        Context.Inventories.Add(invA);
        Context.Inventories.Add(invB);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        var lineId = stockOut.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyPickLocationsCommandHandler(actContext);

        var result = await handler.Handle(
            new ModifyPickLocationsCommand(stockOut.Id, lineId,
                [new PickAllocationRequest(locB.Id, null, 30)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.CannotModifyItems");

        await using var verify = CreateContext();

        // Status guard runs before any reservation move — locA still holds all 30.
        var reloadedA = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invA.Id, ct);
        reloadedA.Reserved.Value.Should().Be(30);
        var reloadedB = await verify.Inventories.AsNoTracking().SingleAsync(i => i.Id == invB.Id, ct);
        reloadedB.Reserved.Value.Should().Be(0);
    }

    [Fact]
    public async Task Missing_stock_out_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new ModifyPickLocationsCommandHandler(actContext);

        var result = await handler.Handle(
            new ModifyPickLocationsCommand(Guid.NewGuid(), Guid.NewGuid(),
                [new PickAllocationRequest(Guid.NewGuid(), null, 1)]),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.NotFound");
    }
}
