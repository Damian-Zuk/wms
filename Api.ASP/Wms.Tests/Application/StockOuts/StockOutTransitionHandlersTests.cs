using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockOuts.Commands;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;


public class StockOutTransitionHandlersTests : IntegrationTestBase
{
    public StockOutTransitionHandlersTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    public class Ship : StockOutTransitionHandlersTests
    {
        public Ship(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Packed_transitions_to_shipped()
        {
            var stockOut = await SeedStockOutAsync(StockOutStatus.Packed);

            await using var actContext = CreateContext();
            var result = await new ShipStockOutCommandHandler(actContext).Handle(
                new ShipStockOutCommand(stockOut.Id),
                TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();
            (await ReloadStatusAsync(stockOut.Id)).Should().Be(StockOutStatus.Shipped);
        }

        [Fact]
        public async Task Wrong_status_is_rejected()
        {
            var stockOut = await SeedStockOutAsync(StockOutStatus.Picking);

            await using var actContext = CreateContext();
            var result = await new ShipStockOutCommandHandler(actContext).Handle(
                new ShipStockOutCommand(stockOut.Id),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }

        [Fact]
        public async Task Missing_stock_out_returns_not_found()
        {
            await using var actContext = CreateContext();
            var result = await new ShipStockOutCommandHandler(actContext).Handle(
                new ShipStockOutCommand(Guid.NewGuid()),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.NotFound");
        }
    }

    public class Complete : StockOutTransitionHandlersTests
    {
        public Complete(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Shipped_transitions_to_completed()
        {
            var stockOut = await SeedStockOutAsync(StockOutStatus.Shipped);

            await using var actContext = CreateContext();
            var result = await new CompleteStockOutCommandHandler(actContext).Handle(
                new CompleteStockOutCommand(stockOut.Id),
                TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();
            (await ReloadStatusAsync(stockOut.Id)).Should().Be(StockOutStatus.Completed);
        }

        [Fact]
        public async Task Wrong_status_is_rejected()
        {
            var stockOut = await SeedStockOutAsync(StockOutStatus.Packed);

            await using var actContext = CreateContext();
            var result = await new CompleteStockOutCommandHandler(actContext).Handle(
                new CompleteStockOutCommand(stockOut.Id),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }

        [Fact]
        public async Task Missing_stock_out_returns_not_found()
        {
            await using var actContext = CreateContext();
            var result = await new CompleteStockOutCommandHandler(actContext).Handle(
                new CompleteStockOutCommand(Guid.NewGuid()),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.NotFound");
        }
    }

    /// <summary>
    /// Seeds a product, location, inventory row, and a StockOut driven
    /// through public domain methods to <paramref name="status"/>. The
    /// inventory row mirrors the lifecycle: Reserved while Picking;
    /// fully picked (OnHand and Reserved both 0) once Packed onwards.
    /// Domain events raised along the way are cleared so the save does not
    /// dispatch them.
    /// </summary>
    private async Task<StockOut> SeedStockOutAsync(StockOutStatus status)
    {
        var product = TestData.Product($"SOT-{Guid.NewGuid():N}"[..10]);
        var location = TestData.Location($"SOT-{Guid.NewGuid():N}"[..10]);
        var quantity = new Quantity(1);

        var inventory = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 1);

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, quantity);

        switch (status)
        {
            case StockOutStatus.Draft:
                break;
            case StockOutStatus.Picking:
                inventory.Reserve(quantity);
                stockOut.StartPicking();
                break;
            case StockOutStatus.Packed:
                inventory.Reserve(quantity);
                stockOut.StartPicking();
                inventory.Pick(quantity);
                stockOut.Pack();
                break;
            case StockOutStatus.Shipped:
                inventory.Reserve(quantity);
                stockOut.StartPicking();
                inventory.Pick(quantity);
                stockOut.Pack();
                stockOut.Ship();
                break;
        }

        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(inventory);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return stockOut;
    }

    private async Task<StockOutStatus> ReloadStatusAsync(Guid id)
    {
        await using var verify = CreateContext();
        var reloaded = await verify.StockOuts
            .AsNoTracking()
            .SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        return reloaded.Status;
    }
}
