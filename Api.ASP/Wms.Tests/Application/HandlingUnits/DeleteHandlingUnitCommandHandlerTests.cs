using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.HandlingUnits.Commands;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.HandlingUnits;

public class DeleteHandlingUnitCommandHandlerTests : IntegrationTestBase
{
    public DeleteHandlingUnitCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Empty_unit_is_soft_deleted()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-DL-1");
        var location = TestData.Location("HU-DL-1");
        var handlingUnit = TestData.HandlingUnit("HU-DL-1-PAL", locationId: location.Id);
        // An emptied row (picked to zero) does not block deletion.
        var emptyRow = TestData.Inventory(product.Id, location.Id, handlingUnitId: handlingUnit.Id);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(emptyRow);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new DeleteHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(new DeleteHandlingUnitCommand(handlingUnit.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        (await verify.HandlingUnits.AnyAsync(h => h.Id == handlingUnit.Id, ct)).Should().BeFalse();
        (await verify.HandlingUnits.IgnoreQueryFilters().SingleAsync(h => h.Id == handlingUnit.Id, ct))
            .IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Unit_holding_stock_cannot_be_deleted()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-DL-2");
        var location = TestData.Location("HU-DL-2");
        var handlingUnit = TestData.HandlingUnit("HU-DL-2-PAL", locationId: location.Id);
        var row = TestData.Inventory(product.Id, location.Id, onHand: 1, handlingUnitId: handlingUnit.Id);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.Inventories.Add(row);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new DeleteHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(new DeleteHandlingUnitCommand(handlingUnit.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.NotEmpty");
    }

    [Fact]
    public async Task Unit_referenced_by_an_active_stock_in_cannot_be_deleted()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-DL-3");
        var location = TestData.Location("HU-DL-3");
        var handlingUnit = TestData.HandlingUnit("HU-DL-3-PAL");
        // Draft stock-in declares the unit; nothing has been put away yet.
        var stockIn = TestData.StockIn(product.Id, location.Id, 10, handlingUnitId: handlingUnit.Id);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new DeleteHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(new DeleteHandlingUnitCommand(handlingUnit.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.InUseByActiveDocuments");
    }

    [Fact]
    public async Task Unit_referenced_by_an_active_stock_out_cannot_be_deleted()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("HU-DL-4");
        var location = TestData.Location("HU-DL-4");
        var handlingUnit = TestData.HandlingUnit("HU-DL-4-PAL", locationId: location.Id);
        var stockOut = TestData.StockOut(product.Id, location.Id, 5, handlingUnitId: handlingUnit.Id);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(handlingUnit);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new DeleteHandlingUnitCommandHandler(actContext);

        var result = await handler.Handle(new DeleteHandlingUnitCommand(handlingUnit.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.InUseByActiveDocuments");
    }
}
