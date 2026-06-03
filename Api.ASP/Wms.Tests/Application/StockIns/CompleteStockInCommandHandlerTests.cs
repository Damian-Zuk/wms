using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockIns.Commands;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockIns;

public class CompleteStockInCommandHandlerTests : IntegrationTestBase
{
    public CompleteStockInCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Putaway_with_all_items_placed_transitions_to_completed()
    {
        var (stockIn, _, _) = await SeedStockInAsync(StockInStatus.Putaway, fullyPlaced: true);

        await using var actContext = CreateContext();
        var handler = new CompleteStockInCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockInCommand(stockIn.Id),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloaded = await verify.StockIns
            .AsNoTracking()
            .SingleAsync(s => s.Id == stockIn.Id, TestContext.Current.CancellationToken);
        reloaded.Status.Should().Be(StockInStatus.Completed);
    }

    [Fact]
    public async Task Rejected_when_not_all_items_placed()
    {
        // In Putaway but nothing has been put away yet.
        var (stockIn, _, _) = await SeedStockInAsync(StockInStatus.Putaway, fullyPlaced: false);

        await using var actContext = CreateContext();
        var handler = new CompleteStockInCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockInCommand(stockIn.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.NotAllItemsPlaced");
    }

    [Fact]
    public async Task Wrong_status_is_rejected()
    {
        // Draft — Complete requires Putaway.
        var (stockIn, _, _) = await SeedStockInAsync(StockInStatus.Draft);

        await using var actContext = CreateContext();
        var handler = new CompleteStockInCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockInCommand(stockIn.Id),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
    }

    [Fact]
    public async Task Missing_stock_in_returns_not_found()
    {
        await using var actContext = CreateContext();
        var handler = new CompleteStockInCommandHandler(actContext);

        var result = await handler.Handle(
            new CompleteStockInCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.NotFound");
    }

    /// <summary>
    /// Seeds a product, location, and a StockIn (driven through public
    /// domain methods to <paramref name="status"/>) so the StockInItem
    /// foreign keys resolve when SaveChanges hits the database. When
    /// <paramref name="fullyPlaced"/> is set, every placement is put away.
    /// </summary>
    private async Task<(StockIn StockIn, Product Product, Location Location)>
        SeedStockInAsync(StockInStatus status, bool fullyPlaced = false)
    {
        var product = TestData.Product($"CSI-{Guid.NewGuid():N}"[..10]);
        var location = TestData.Location($"CSI-LOC-{Guid.NewGuid():N}"[..10]);

        var stockIn = new StockIn(Guid.NewGuid());
        stockIn.AddLineWithPlacements(
            product.Id,
            null,
            new Quantity(1),
            [new(location.Id, 1, PutawayStrategyType.NearestEmpty)]);

        switch (status)
        {
            case StockInStatus.Draft:
                break;
            case StockInStatus.Putaway:
                stockIn.StartPutaway();
                if (fullyPlaced)
                    PutawayAll(stockIn);
                break;
            case StockInStatus.Completed:
                stockIn.StartPutaway();
                PutawayAll(stockIn);
                stockIn.Complete();
                break;
            case StockInStatus.Cancelled:
                stockIn.Cancel();
                break;
        }

        stockIn.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (stockIn, product, location);
    }

    private static void PutawayAll(StockIn stockIn)
    {
        foreach (var line in stockIn.Lines)
            foreach (var item in line.Items)
                stockIn.PutawayItem(item.Id, item.Quantity);
    }
}
