using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Infrastructure.Putaway;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockIns;

public class StartPutawayStockInCommandHandlerTests : IntegrationTestBase
{
    public StartPutawayStockInCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Reserves_capacity_and_transitions_to_putaway()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("SR-1");
        var location = TestData.Location("SR-1-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 30);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var service = new CapacityReservationService(actContext);

        var result = await service.ReserveForStartPutawayAsync(stockIn.Id, ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reloaded = await verify.StockIns.AsNoTracking().SingleAsync(s => s.Id == stockIn.Id, ct);
        reloaded.Status.Should().Be(StockInStatus.Putaway);

        var reservations = await verify.CapacityReservations
            .AsNoTracking()
            .Where(r => r.StockInId == stockIn.Id)
            .ToListAsync(ct);
        reservations.Should().ContainSingle().Which.Should().Match<CapacityReservation>(r =>
            r.LocationId == location.Id
            && r.Quantity.Value == 30);
    }

    [Fact]
    public async Task Fails_and_stays_draft_when_capacity_no_longer_available()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("SR-2");
        var location = TestData.Location("SR-2-LOC", capacity: 100);
        var stockIn = TestData.StockIn(product.Id, location.Id, 80);

        // Someone else's on-hand stock fills most of the location after planning.
        var foreignStock = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 50);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.Inventories.Add(foreignStock);
        Context.StockIns.Add(stockIn);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var service = new CapacityReservationService(actContext);

        var result = await service.ReserveForStartPutawayAsync(stockIn.Id, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.CapacityExceeded");

        await using var verify = CreateContext();
        var reloaded = await verify.StockIns.AsNoTracking().SingleAsync(s => s.Id == stockIn.Id, ct);
        reloaded.Status.Should().Be(StockInStatus.Draft);
        (await verify.CapacityReservations.AnyAsync(r => r.StockInId == stockIn.Id, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Two_stock_ins_racing_for_the_same_location_only_one_wins()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("SR-3");
        var location = TestData.Location("SR-3-LOC", capacity: 100);

        // Each wants 60 — individually fine, together 120 > 100.
        var first = TestData.StockIn(product.Id, location.Id, 60);
        var second = TestData.StockIn(product.Id, location.Id, 60);

        Context.Products.Add(product);
        Context.Locations.Add(location);
        Context.StockIns.Add(first);
        Context.StockIns.Add(second);
        await Context.SaveChangesAsync(ct);

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();
        var serviceA = new CapacityReservationService(contextA);
        var serviceB = new CapacityReservationService(contextB);

        var results = await Task.WhenAll(
            serviceA.ReserveForStartPutawayAsync(first.Id, ct),
            serviceB.ReserveForStartPutawayAsync(second.Id, ct));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => r.IsFailure).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Code.Should().Be("Location.CapacityExceeded");

        await using var verify = CreateContext();
        var putawayCount = await verify.StockIns
            .AsNoTracking()
            .CountAsync(s => s.Status == StockInStatus.Putaway, ct);
        putawayCount.Should().Be(1);

        var reservationCount = await verify.CapacityReservations
            .AsNoTracking()
            .CountAsync(ct);
        reservationCount.Should().Be(1);
    }
}
