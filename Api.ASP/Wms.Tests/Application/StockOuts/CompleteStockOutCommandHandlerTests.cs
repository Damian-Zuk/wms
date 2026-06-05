using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockOuts.Commands;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;

public class CompleteStockOutCommandHandlerTests : IntegrationTestBase
{
    public CompleteStockOutCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Picking_with_all_items_picked_transitions_to_completed()
    {
        var stockOut = await SeedStockOutAsync(StockOutStatus.Picking, fullyPicked: true);

        await using var actContext = CreateContext();
        var handler = new CompleteStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        (await ReloadStatusAsync(stockOut.Id)).Should().Be(StockOutStatus.Completed);
    }

    [Fact]
    public async Task Rejected_when_not_all_items_picked()
    {
        // In Picking but nothing has been picked yet.
        var stockOut = await SeedStockOutAsync(StockOutStatus.Picking, fullyPicked: false);

        await using var actContext = CreateContext();
        var handler = new CompleteStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.NotAllItemsPicked");
    }

    [Fact]
    public async Task Wrong_status_is_rejected()
    {
        // Draft — Complete requires Picking.
        var stockOut = await SeedStockOutAsync(StockOutStatus.Draft);

        await using var actContext = CreateContext();
        var handler = new CompleteStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockOutCommand(stockOut.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.InvalidStatusTransition");
    }

    [Fact]
    public async Task Missing_stock_out_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new CompleteStockOutCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockOutCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockOut.NotFound");
    }

    /// <summary>
    /// Seeds a product, location, and a StockOut driven through public domain
    /// methods to <paramref name="status"/> so the StockOutItem foreign keys
    /// resolve on save. When <paramref name="fullyPicked"/> is set, every item
    /// is fully picked. Domain events are cleared so the save does not dispatch them.
    /// </summary>
    private async Task<StockOut> SeedStockOutAsync(StockOutStatus status, bool fullyPicked = false)
    {
        var product = TestData.Product($"CSO-{Guid.NewGuid():N}"[..10]);
        var location = TestData.Location($"CSO-LOC-{Guid.NewGuid():N}"[..10]);

        var stockOut = TestData.StockOut(product.Id, location.Id, 1);

        switch (status)
        {
            case StockOutStatus.Draft:
                break;
            case StockOutStatus.Picking:
                stockOut.StartPicking();
                if (fullyPicked)
                    PickAll(stockOut);
                break;
        }

        stockOut.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockOuts.Add(stockOut);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return stockOut;
    }

    private static void PickAll(StockOut stockOut)
    {
        foreach (var line in stockOut.Lines)
            foreach (var item in line.Items)
                stockOut.PickItem(item.Id, item.Quantity);
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
