using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.StockOuts.Commands;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;

/// <summary>
/// Status-transition handlers (Pack, Ship, Complete) are thin domain-method
/// wrappers; the happy path is one transition, plus wrong-status and
/// not-found.
/// </summary>
public class StockOutTransitionHandlersTests : IntegrationTestBase
{
    public StockOutTransitionHandlersTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    public class Pack : StockOutTransitionHandlersTests
    {
        public Pack(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Picking_transitions_to_packed()
        {
            var stockOut = await SeedStockOutAsync(StockOutStatus.Picking);

            await using var actContext = CreateContext();
            var result = await new PackStockOutCommandHandler(actContext).Handle(
                new PackStockOutCommand(stockOut.Id),
                TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();
            (await ReloadStatusAsync(stockOut.Id)).Should().Be(StockOutStatus.Packed);
        }

        [Fact]
        public async Task Wrong_status_is_rejected()
        {
            var stockOut = await SeedStockOutAsync(StockOutStatus.Draft);

            await using var actContext = CreateContext();
            var result = await new PackStockOutCommandHandler(actContext).Handle(
                new PackStockOutCommand(stockOut.Id),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
        }

        [Fact]
        public async Task Missing_stock_out_returns_not_found()
        {
            await using var actContext = CreateContext();
            var result = await new PackStockOutCommandHandler(actContext).Handle(
                new PackStockOutCommand(Guid.NewGuid()),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.NotFound");
        }
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
    /// Seeds a product, location, and a StockOut driven through public
    /// domain methods to <paramref name="status"/>. Seeding the FK targets
    /// is required because StockOutItem has Restrict-FKs to Product/Location.
    /// Domain events raised along the way are cleared so the save does not
    /// dispatch them.
    /// </summary>
    private async Task<StockOut> SeedStockOutAsync(StockOutStatus status)
    {
        var product = TestData.Product($"SOT-{Guid.NewGuid():N}"[..10]);
        var location = TestData.Location($"SOT-{Guid.NewGuid():N}"[..10]);

        var stockOut = new StockOut(Guid.NewGuid());
        stockOut.AddItem(product.Id, location.Id, null, new Quantity(1));

        switch (status)
        {
            case StockOutStatus.Draft:
                break;
            case StockOutStatus.Picking:
                stockOut.StartPicking();
                break;
            case StockOutStatus.Packed:
                stockOut.StartPicking();
                stockOut.Pack();
                break;
            case StockOutStatus.Shipped:
                stockOut.StartPicking();
                stockOut.Pack();
                stockOut.Ship();
                break;
        }

        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
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
