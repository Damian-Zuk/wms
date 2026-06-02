using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.StockIns.Commands;
using Wms.Domain.Enums;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockIns;

public class ModifyStockInPlacementsCommandHandlerTests : IntegrationTestBase
{
    public ModifyStockInPlacementsCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Re_split_keeps_total_marks_manual_and_records_modifier()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MD-1");
        var locA = TestData.Location("MD-1-A", capacity: 100);
        var locB = TestData.Location("MD-1-B", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, locA.Id, 30);

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Locations.Add(locB);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var lineId = stockIn.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyStockInLinePlacementsCommandHandler(actContext, CurrentUser);

        var result = await handler.Handle(
            new ModifyStockInLinePlacementsCommand(stockIn.Id, lineId,
                [new ModifyPlacementRequest(locA.Id, 10), new ModifyPlacementRequest(locB.Id, 20)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloaded = await verify.StockIns
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == stockIn.Id, ct);

        var line = reloaded.Lines.Single();
        line.Items.Should().HaveCount(2);
        line.Items.Sum(i => i.Quantity.Value).Should().Be(30);
        line.Items.Should().OnlyContain(i => i.Strategy == PutawayStrategyType.Manual);
        reloaded.ModifiedBy.Should().Be(CurrentUser.UserName);
        reloaded.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Total_mismatch_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MD-2");
        var locA = TestData.Location("MD-2-A", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, locA.Id, 30);

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var lineId = stockIn.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyStockInLinePlacementsCommandHandler(actContext, CurrentUser);

        var result = await handler.Handle(
            new ModifyStockInLinePlacementsCommand(stockIn.Id, lineId,
                [new ModifyPlacementRequest(locA.Id, 25)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.PlacementsDoNotMatchLineTotal");
    }

    [Fact]
    public async Task Target_that_cannot_accept_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MD-3");
        var locA = TestData.Location("MD-3-A", capacity: 100);
        var locTight = TestData.Location("MD-3-TIGHT", capacity: 5);
        var stockIn = TestData.StockIn(product.Id, locA.Id, 30);

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Locations.Add(locTight);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var lineId = stockIn.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyStockInLinePlacementsCommandHandler(actContext, CurrentUser);

        var result = await handler.Handle(
            new ModifyStockInLinePlacementsCommand(stockIn.Id, lineId,
                [new ModifyPlacementRequest(locA.Id, 10), new ModifyPlacementRequest(locTight.Id, 20)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.CapacityExceeded");
    }

    [Fact]
    public async Task Rejected_outside_draft()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("MD-4");
        var locA = TestData.Location("MD-4-A", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, locA.Id, 30);
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var lineId = stockIn.Lines.Single().Id;

        await using var actContext = CreateContext();
        var handler = new ModifyStockInLinePlacementsCommandHandler(actContext, CurrentUser);

        var result = await handler.Handle(
            new ModifyStockInLinePlacementsCommand(stockIn.Id, lineId,
                [new ModifyPlacementRequest(locA.Id, 30)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.CannotModifyItems");
    }
}
