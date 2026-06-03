using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockIns.Commands;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockIns;

public class CancelStockInCommandHandlerTests : IntegrationTestBase
{
    public CancelStockInCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Cancel_from_receiving_deletes_active_reservations()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-1");
        var location = TestData.Location("CN-1-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 10);
        stockIn.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        var item = stockIn.Lines.Single().Items.Single();
        var reservation = new CapacityReservation(
            stockIn.Id, item.Id, location.Id, product.Id, null, new Quantity(10));
        Context.CapacityReservations.Add(reservation);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloadedStockIn = await verify.StockIns.AsNoTracking().SingleAsync(s => s.Id == stockIn.Id, ct);
        reloadedStockIn.Status.Should().Be(StockInStatus.Cancelled);

        (await verify.CapacityReservations.AnyAsync(r => r.StockInId == stockIn.Id, ct))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_from_draft_succeeds_with_no_reservations()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-2");
        var location = TestData.Location("CN-2-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 10);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloaded = await verify.StockIns.AsNoTracking().SingleAsync(s => s.Id == stockIn.Id, ct);
        reloaded.Status.Should().Be(StockInStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_after_received_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CN-3");
        var location = TestData.Location("CN-3-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 10);
        stockIn.StartReceiving();
        stockIn.Receive();
        stockIn.ClearDomainEvents();

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CancelStockInCommandHandler(actContext);

        var result = await handler.Handle(new CancelStockInCommand(stockIn.Id), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.InvalidStatusTransition");
    }
}
