using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.StockIns.Commands;
using Wms.Application.Putaway;
using Wms.Application.Putaway.Strategies;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Wms.Infrastructure.HandlingUnits;
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
        new NearestEmptyAllocationStrategy(),
        new NearestAvailableAllocationStrategy()
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
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 150)], null),
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
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 150)], null),
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
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 500)], null),
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
        other.StartPutaway();

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
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(product.Id, null, 50)], null),
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
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        var result = await handler.Handle(
            new CreateStockInCommand([new StockInLineRequest(Guid.NewGuid(), null, 5)], null),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.ProductNotFound");
    }

    [Fact]
    public async Task Declared_handling_units_become_unplaced_units_with_one_placement_each()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CR-HU-1");
        var locA = TestData.Location("CR-HU-1-A", capacity: 60);
        var locB = TestData.Location("CR-HU-1-B", capacity: 60);
        var locC = TestData.Location("CR-HU-1-C", capacity: 100);
        Context.Products.AddRange(product);
        Context.Locations.AddRange(locA, locB, locC);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        // 150 = two pallets of 50 + 50 loose.
        var result = await handler.Handle(
            new CreateStockInCommand(
            [
                new StockInLineRequest(product.Id, null, 150, [
                    new DeclaredHandlingUnitRequest(50, HandlingUnitType.Pallet),
                    new DeclaredHandlingUnitRequest(50, HandlingUnitType.Pallet)
                ])
            ], null),
            ct);

        result.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var stockIn = await verify.StockIns
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Items)
            .SingleAsync(s => s.Id == result.Value, ct);

        var line = stockIn.Lines.Should().ContainSingle().Subject;
        var huItems = line.Items.Where(i => i.HandlingUnitId.HasValue).ToList();
        huItems.Should().HaveCount(2);
        huItems.Should().OnlyContain(i => i.Quantity.Value == 50);
        huItems.Select(i => i.HandlingUnitId).Distinct().Should().HaveCount(2);
        line.Items.Where(i => i.HandlingUnitId == null).Sum(i => i.Quantity.Value).Should().Be(50);

        var handlingUnits = await verify.HandlingUnits.AsNoTracking().ToListAsync(ct);
        handlingUnits.Should().HaveCount(2);
        handlingUnits.Should().OnlyContain(h => h.LocationId == null, "units are placed at putaway, not at creation");
        handlingUnits.Should().OnlyContain(h => h.Code.Value.StartsWith("HU-"));
        handlingUnits.Select(h => h.Code.Value).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Handling_unit_chunks_are_never_split_across_locations()
    {
        var ct = TestContext.Current.CancellationToken;

        // Two bins of 60: loose planning would split 100 across them, a pallet of 100 must not.
        var product = TestData.Product("CR-HU-2");
        var locA = TestData.Location("CR-HU-2-A", capacity: 60);
        var locB = TestData.Location("CR-HU-2-B", capacity: 60);
        Context.Products.AddRange(product);
        Context.Locations.AddRange(locA, locB);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        var result = await handler.Handle(
            new CreateStockInCommand(
            [
                new StockInLineRequest(product.Id, null, 100, [
                    new DeclaredHandlingUnitRequest(100, HandlingUnitType.Pallet)
                ])
            ], null),
            ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Putaway.NoSingleLocationFitsHandlingUnit");

        await using var verify = CreateContext();
        (await verify.StockIns.AnyAsync(ct)).Should().BeFalse();
        (await verify.HandlingUnits.IgnoreQueryFilters().AnyAsync(ct)).Should().BeFalse(
            "a failed create must not leak declared units");
    }

    [Fact]
    public async Task Manual_code_is_used_and_duplicates_are_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var product = TestData.Product("CR-HU-3");
        var loc = TestData.Location("CR-HU-3-A", capacity: null);
        Context.Products.Add(product);
        Context.Locations.Add(loc);
        Context.HandlingUnits.Add(TestData.HandlingUnit(code: "PAL-TAKEN"));
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateStockInCommandHandler(actContext, Planner(), new HandlingUnitCodeGenerator(actContext));

        var taken = await handler.Handle(
            new CreateStockInCommand(
            [
                new StockInLineRequest(product.Id, null, 10, [
                    new DeclaredHandlingUnitRequest(10, HandlingUnitType.Box, "PAL-TAKEN")
                ])
            ], null),
            ct);

        taken.IsFailure.Should().BeTrue();
        taken.Error.Code.Should().Be("HandlingUnit.CodeAlreadyExists");

        var ok = await handler.Handle(
            new CreateStockInCommand(
            [
                new StockInLineRequest(product.Id, null, 10, [
                    new DeclaredHandlingUnitRequest(10, HandlingUnitType.Box, "PAL-FRESH")
                ])
            ], null),
            ct);

        ok.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        (await verify.HandlingUnits.AnyAsync(h => h.Code.Value == "PAL-FRESH", ct)).Should().BeTrue();
    }
}
