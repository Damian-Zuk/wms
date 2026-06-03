using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockIns.Commands;
using Wms.Application.Putaway;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockIns;

public class CreateStockInCommandHandlerTests : IntegrationTestBase
{
    public CreateStockInCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private static IPutawayPlanner Planner() => new PutawayPlanner(
    [
        new PreferredLocationAllocationStrategy(),
        new ConsolidateSameSkuAllocationStrategy(),
        new NearestEmptyAllocationStrategy()
    ]);

    [Fact]
    public async Task Splits_quantity_across_multiple_locations_to_capacity()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CR-1");
        var locA = TestData.Location("CR-1-A", capacity: 100);
        var locB = TestData.Location("CR-1-B", capacity: 100);
        Context.Products.Add(product);
        Context.Locations.Add(locA);
        Context.Locations.Add(locB);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 150)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var stockIn = await verify.StockIns
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == result.Value, ct);

        var line = stockIn.Lines.Should().ContainSingle().Subject;
        line.Items.Should().HaveCount(2);
        line.Items.Sum(i => i.Quantity.Value).Should().Be(150);
        line.Items.Select(i => i.Quantity.Value).OrderBy(x => x).Should().Equal(50, 100);
        line.Items.Should().OnlyContain(i => i.Strategy == PutawayStrategyType.NearestEmpty);
    }

    [Fact]
    public async Task Fails_and_persists_nothing_when_capacity_is_insufficient()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CR-2");
        var loc = TestData.Location("CR-2-ONLY", capacity: 100);
        Context.Products.Add(product);
        Context.Locations.Add(loc);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 150)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Putaway.CannotPlaceFullQuantity");

        await using var verify = CreateContext();
        (await verify.StockIns.AnyAsync(ct)).Should().BeFalse();
        (await verify.StockInLines.AnyAsync(ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Unlimited_location_absorbs_full_quantity_in_one_placement()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CR-3");
        var loc = TestData.Location("CR-3-INF", capacity: null);
        Context.Products.Add(product);
        Context.Locations.Add(loc);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 500)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var stockIn = await verify.StockIns
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == result.Value, ct);

        var line = stockIn.Lines.Should().ContainSingle().Subject;
        var placement = line.Items.Should().ContainSingle().Subject;
        placement.LocationId.Should().Be(loc.Id);
        placement.Quantity.Value.Should().Be(500);
    }

    [Fact]
    public async Task Routes_around_capacity_reserved_by_another_stock_in()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CR-4");
        var reservedLoc = TestData.Location("CR-4-RES", capacity: 100);
        var freeLoc = TestData.Location("CR-4-FREE", capacity: 100);

        // A started stock-in already holding a full reservation on reservedLoc.
        var other = TestData.StockIn(product.Id, reservedLoc.Id, 100);
        other.StartReceiving();

        Context.Products.Add(product);
        Context.Locations.Add(reservedLoc);
        Context.Locations.Add(freeLoc);
        Context.StockIns.Add(other);
        await Context.SaveChangesAsync(ct);

        var otherItem = other.Lines.Single().Items.Single();
        Context.CapacityReservations.Add(new CapacityReservation(
            other.Id, otherItem.Id, reservedLoc.Id, product.Id, null, new Quantity(100)));
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 50)]),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var stockIn = await verify.StockIns
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == result.Value, ct);

        var placement = stockIn.Lines.Single().Items.Should().ContainSingle().Subject;
        placement.LocationId.Should().Be(freeLoc.Id);
    }

    [Fact]
    public async Task Unknown_product_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner());

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(Guid.NewGuid(), null, 5)]),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.ProductNotFound");
    }
}
