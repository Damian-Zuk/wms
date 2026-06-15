using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Common.Data;
using Wms.Application.Handlers.StockMovements.Events;
using Wms.Application.Handlers.StockTransfers.Commands;
using Wms.Domain.Events;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockTransfers;

/// <summary>
/// Transfers in and out of handling units: a partial quantity can leave a pallet for
/// another location (or land on a pallet standing at the destination) without the
/// unpack → transfer → pack dance.
/// </summary>
public class TransferStockHandlingUnitTests : IntegrationTestBase
{
    public TransferStockHandlingUnitTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Transfer_out_of_a_unit_lands_as_loose_stock_and_movements_carry_the_unit()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("XF-HU-1");
        var source = TestData.Location("XF-HU-1-SRC");
        var destination = TestData.Location("XF-HU-1-DST");
        var handlingUnit = TestData.HandlingUnit("XF-HU-1-PAL", locationId: source.Id);
        var unitRow = TestData.Inventory(product.Id, source.Id, onHand: 10, handlingUnitId: handlingUnit.Id);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(unitRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        WireTransferredHandler(actContext);
        var handler = new TransferStockCommandHandler(actContext);

        var result = await handler.Handle(
            new TransferStockCommand(
                product.Id, source.Id, destination.Id, LotId: null, Quantity: 4,
                SourceHandlingUnitId: handlingUnit.Id),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();

        var rows = await verify.Inventories.AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .ToListAsync(ct);
        rows.Single(r => r.HandlingUnitId == handlingUnit.Id).OnHand.Value.Should().Be(6);
        var landed = rows.Single(r => r.LocationId == destination.Id);
        landed.HandlingUnitId.Should().BeNull();
        landed.OnHand.Value.Should().Be(4);

        var movements = await verify.StockMovements.AsNoTracking().ToListAsync(ct);
        movements.Should().HaveCount(2);
        movements.Single(m => m.LocationId == source.Id).HandlingUnitId.Should().Be(handlingUnit.Id);
        movements.Single(m => m.LocationId == destination.Id).HandlingUnitId.Should().BeNull();
    }

    [Fact]
    public async Task Transfer_into_a_unit_standing_at_the_destination_succeeds()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("XF-HU-2");
        var source = TestData.Location("XF-HU-2-SRC");
        var destination = TestData.Location("XF-HU-2-DST");
        var handlingUnit = TestData.HandlingUnit("XF-HU-2-PAL", locationId: destination.Id);
        var looseRow = TestData.Inventory(product.Id, source.Id, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(looseRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new TransferStockCommandHandler(actContext);

        var result = await handler.Handle(
            new TransferStockCommand(
                product.Id, source.Id, destination.Id, LotId: null, Quantity: 5,
                DestinationHandlingUnitId: handlingUnit.Id),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var landed = await verify.Inventories.AsNoTracking()
            .SingleAsync(i => i.LocationId == destination.Id && i.ProductId == product.Id, ct);
        landed.HandlingUnitId.Should().Be(handlingUnit.Id);
        landed.OnHand.Value.Should().Be(5);
    }

    [Fact]
    public async Task Destination_unit_standing_elsewhere_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("XF-HU-3");
        var source = TestData.Location("XF-HU-3-SRC");
        var destination = TestData.Location("XF-HU-3-DST");
        var elsewhere = TestData.Location("XF-HU-3-ELSE");
        var handlingUnit = TestData.HandlingUnit("XF-HU-3-PAL", locationId: elsewhere.Id);
        var looseRow = TestData.Inventory(product.Id, source.Id, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination, elsewhere);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(looseRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new TransferStockCommandHandler(actContext);

        var result = await handler.Handle(
            new TransferStockCommand(
                product.Id, source.Id, destination.Id, LotId: null, Quantity: 5,
                DestinationHandlingUnitId: handlingUnit.Id),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.NotAtLocation");
    }

    [Fact]
    public async Task Source_unit_row_must_exist()
    {
        var ct = TestContext.Current.CancellationToken;

        // Loose stock exists, but the command names a pallet that holds none.
        var product = TestData.Product("XF-HU-4");
        var source = TestData.Location("XF-HU-4-SRC");
        var destination = TestData.Location("XF-HU-4-DST");
        var handlingUnit = TestData.HandlingUnit("XF-HU-4-PAL", locationId: source.Id);
        var looseRow = TestData.Inventory(product.Id, source.Id, onHand: 10);

        Context.Products.Add(product);
        Context.Locations.AddRange(source, destination);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(looseRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new TransferStockCommandHandler(actContext);

        var result = await handler.Handle(
            new TransferStockCommand(
                product.Id, source.Id, destination.Id, LotId: null, Quantity: 5,
                SourceHandlingUnitId: handlingUnit.Id),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockTransfer.SourceInventoryNotFound");
    }

    private void WireTransferredHandler(IAppDbContext context)
    {
        EventDispatcher.Register<StockTransferredDomainEvent>((evt, ct) =>
            new StockTransferredDomainEventHandler(context).Handle(evt, ct));
    }
}
