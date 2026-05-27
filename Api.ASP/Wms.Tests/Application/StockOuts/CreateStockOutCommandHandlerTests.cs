using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Allocations;
using Wms.Application.Features.StockOuts.Commands;
using Wms.Domain.Enums;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.StockOuts;

public class CreateStockOutCommandHandlerTests : IntegrationTestBase
{
    public CreateStockOutCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    public class FefoBranch : CreateStockOutCommandHandlerTests
    {
        public FefoBranch(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Allocates_from_earliest_expiring_lot()
        {
            // Arrange: one product with two lots in the same location. Caller
            // doesn't specify a LotId, so FEFO must pick the earliest.
            var product = TestData.Product("SO-PROD");
            var location = TestData.Location("SO-LOC");

            var earlyLot = TestData.Lot(product.Id, "EARLY", new DateOnly(2026, 06, 01));
            var lateLot = TestData.Lot(product.Id, "LATE", new DateOnly(2027, 06, 01));

            var earlyInv = TestData.Inventory(product.Id, location.Id, earlyLot.Id, onHand: 8);
            var lateInv = TestData.Inventory(product.Id, location.Id, lateLot.Id, onHand: 10);

            Context.Products.Add(product);
            Context.Locations.Add(location);
            Context.Lots.AddRange(earlyLot, lateLot);
            Context.Inventories.AddRange(earlyInv, lateInv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act: pull 12 units, expecting 8 from early (drained) + 4 from late.
            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));
            var command = new CreateStockOutCommand(new List<StockOutItemRequest>
            {
                new(product.Id, location.Id, LotId: null, Quantity: 12)
            });

            var result = await handler.Handle(command, TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();

            var ct = TestContext.Current.CancellationToken;

            await using var verify = CreateContext();
            var stockOut = await verify.StockOuts
                .Include(s => s.Items)
                .SingleAsync(s => s.Id == result.Value, ct);

            stockOut.Status.Should().Be(StockOutStatus.Draft);
            stockOut.Items.Should().HaveCount(2);

            var itemByLot = stockOut.Items.ToDictionary(i => i.LotId!.Value, i => i.Quantity.Value);
            itemByLot[earlyLot.Id].Should().Be(8);
            itemByLot[lateLot.Id].Should().Be(4);

            var inventories = await verify.Inventories
                .AsNoTracking()
                .Where(i => i.ProductId == product.Id)
                .ToListAsync(ct);

            var earlyAfter = inventories.Single(i => i.LotId == earlyLot.Id);
            earlyAfter.OnHand.Value.Should().Be(8);
            earlyAfter.Reserved.Value.Should().Be(8);
            earlyAfter.Available.Value.Should().Be(0);

            var lateAfter = inventories.Single(i => i.LotId == lateLot.Id);
            lateAfter.OnHand.Value.Should().Be(10);
            lateAfter.Reserved.Value.Should().Be(4);
            lateAfter.Available.Value.Should().Be(6);
        }
    }

    public class NonFefoBranch : CreateStockOutCommandHandlerTests
    {
        public NonFefoBranch(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Explicit_lot_reserves_against_the_matching_inventory_row_only()
        {
            // Same product has two lots, but caller pins the later lot
            // explicitly — FEFO must NOT override an explicit LotId.
            var product = TestData.Product("SO-EXPL");
            var location = TestData.Location("SO-EXPL-LOC");
            var earlyLot = TestData.Lot(product.Id, "EARLY", new DateOnly(2026, 06, 01));
            var pinnedLot = TestData.Lot(product.Id, "PIN", new DateOnly(2027, 06, 01));
            var earlyInv = TestData.Inventory(product.Id, location.Id, earlyLot.Id, onHand: 5);
            var pinnedInv = TestData.Inventory(product.Id, location.Id, pinnedLot.Id, onHand: 5);

            Context.Products.Add(product);
            Context.Locations.Add(location);
            Context.Lots.AddRange(earlyLot, pinnedLot);
            Context.Inventories.AddRange(earlyInv, pinnedInv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(product.Id, location.Id, LotId: pinnedLot.Id, Quantity: 3)
                }),
                TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();

            await using var verify = CreateContext();
            var ct = TestContext.Current.CancellationToken;

            var inventories = await verify.Inventories
                .AsNoTracking()
                .Where(i => i.ProductId == product.Id)
                .ToListAsync(ct);

            inventories.Single(i => i.LotId == earlyLot.Id).Reserved.Value.Should().Be(0);
            inventories.Single(i => i.LotId == pinnedLot.Id).Reserved.Value.Should().Be(3);
        }

        [Fact]
        public async Task Non_lot_tracked_product_reserves_against_the_lotless_inventory_row()
        {
            // Product has no lots at all → handler bypasses FEFO entirely
            // and reserves the row with LotId == null.
            var product = TestData.Product("SO-NOLOT");
            var location = TestData.Location("SO-NOLOT-LOC");
            var inv = TestData.Inventory(product.Id, location.Id, lotId: null, onHand: 8);

            Context.Products.Add(product);
            Context.Locations.Add(location);
            Context.Inventories.Add(inv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(product.Id, location.Id, LotId: null, Quantity: 5)
                }),
                TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();

            await using var verify = CreateContext();
            var ct = TestContext.Current.CancellationToken;

            var reloaded = await verify.Inventories
                .AsNoTracking()
                .SingleAsync(i => i.Id == inv.Id, ct);
            reloaded.OnHand.Value.Should().Be(8);
            reloaded.Reserved.Value.Should().Be(5);

            var stockOut = await verify.StockOuts
                .Include(s => s.Items)
                .SingleAsync(s => s.Id == result.Value, ct);
            stockOut.Items.Should().ContainSingle()
                .Which.LotId.Should().BeNull();
        }
    }

    public class FailurePaths : CreateStockOutCommandHandlerTests
    {
        public FailurePaths(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Missing_product_returns_product_not_found()
        {
            var location = TestData.Location("SO-FAIL-LOC-1");
            Context.Locations.Add(location);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(Guid.NewGuid(), location.Id, null, 1)
                }),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.ProductNotFound");
        }

        [Fact]
        public async Task Missing_location_returns_location_not_found()
        {
            var product = TestData.Product("SO-FAIL-2");
            Context.Products.Add(product);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(product.Id, Guid.NewGuid(), null, 1)
                }),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.LocationNotFound");
        }

        [Fact]
        public async Task Missing_lot_returns_lot_not_found()
        {
            var product = TestData.Product("SO-FAIL-3");
            var location = TestData.Location("SO-FAIL-LOC-3");
            Context.Products.Add(product);
            Context.Locations.Add(location);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(product.Id, location.Id, LotId: Guid.NewGuid(), Quantity: 1)
                }),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("StockOut.LotNotFound");
        }

        [Fact]
        public async Task Missing_inventory_row_returns_insufficient_available_stock()
        {
            // Product and location exist, but no inventory row covers them
            // and the product has no lots → non-FEFO branch, no matching row.
            var product = TestData.Product("SO-FAIL-4");
            var location = TestData.Location("SO-FAIL-LOC-4");
            Context.Products.Add(product);
            Context.Locations.Add(location);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(product.Id, location.Id, null, 1)
                }),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");
        }

        [Fact]
        public async Task Insufficient_available_stock_returns_failure_and_persists_nothing()
        {
            // Non-FEFO branch: enough OnHand but not enough Available.
            var product = TestData.Product("SO-FAIL-5");
            var location = TestData.Location("SO-FAIL-LOC-5");
            var inv = TestData.Inventory(product.Id, location.Id, null, onHand: 5);
            inv.Reserve(new Wms.Domain.ValueObjects.Quantity(4));
            Context.Products.Add(product);
            Context.Locations.Add(location);
            Context.Inventories.Add(inv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(product.Id, location.Id, null, Quantity: 3)
                }),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientAvailableStock");

            await using var verify = CreateContext();
            var ct = TestContext.Current.CancellationToken;

            // Nothing persisted.
            (await verify.StockOuts.AsNoTracking().AnyAsync(ct)).Should().BeFalse();
            var unchanged = await verify.Inventories.AsNoTracking()
                .SingleAsync(i => i.Id == inv.Id, ct);
            unchanged.Reserved.Value.Should().Be(4);
        }

        [Fact]
        public async Task Fefo_branch_with_insufficient_total_available_returns_fefo_failure()
        {
            var product = TestData.Product("SO-FAIL-6");
            var location = TestData.Location("SO-FAIL-LOC-6");
            var lot = TestData.Lot(product.Id, "LOT", new DateOnly(2026, 06, 01));
            var inv = TestData.Inventory(product.Id, location.Id, lot.Id, onHand: 2);

            Context.Products.Add(product);
            Context.Locations.Add(location);
            Context.Lots.Add(lot);
            Context.Inventories.Add(inv);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateStockOutCommandHandler(actContext, new FefoAllocator(actContext));

            var result = await handler.Handle(
                new CreateStockOutCommand(new List<StockOutItemRequest>
                {
                    new(product.Id, location.Id, LotId: null, Quantity: 10)
                }),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Inventory.InsufficientAvailableStockForFefo");
        }
    }
}
