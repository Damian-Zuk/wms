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

    [Fact]
    public async Task Fefo_branch_allocates_from_earliest_expiring_lot()
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

        // Assert: stock-out exists and is in Draft, with two items split
        // across lots in FEFO order.
        await using var verify = CreateContext();
        var stockOut = await verify.StockOuts
            .Include(s => s.Items)
            .SingleAsync(s => s.Id == result.Value, ct);

        stockOut.Status.Should().Be(StockOutStatus.Draft);
        stockOut.Items.Should().HaveCount(2);

        var itemByLot = stockOut.Items.ToDictionary(i => i.LotId!.Value, i => i.Quantity.Value);
        itemByLot[earlyLot.Id].Should().Be(8);
        itemByLot[lateLot.Id].Should().Be(4);

        // Assert: inventory rows reflect reserved quantities (OnHand unchanged).
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
