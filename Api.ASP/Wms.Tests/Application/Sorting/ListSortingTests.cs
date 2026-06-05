using FluentAssertions;
using Wms.Application.Handlers.Inventories.Queries;
using Wms.Application.Handlers.Locations.Queries;
using Wms.Application.Handlers.Lots.Queries;
using Wms.Application.Handlers.Products.Queries;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Sorting;

/// <summary>
/// Verifies the sort parameter on each list handler: the chosen column drives
/// ordering, the direction flag flips it, and an unrecognized/absent sort key
/// falls back to the handler's default order.
/// </summary>
public class ListSortingTests : IntegrationTestBase
{
    public ListSortingTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Products_sort_by_sku_ascending_and_descending()
    {
        var ct = TestContext.Current.CancellationToken;
        Context.Products.AddRange(
            TestData.Product("B-2", "Apple"),
            TestData.Product("A-1", "Cherry"),
            TestData.Product("C-3", "Banana"));
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListProductsQueryHandler(queryContext);

        var asc = await handler.Handle(new ListProductsQuery(null, "sku"), ct);
        asc.Value.Items.Select(p => p.Sku).Should().ContainInOrder("A-1", "B-2", "C-3");

        var desc = await handler.Handle(new ListProductsQuery(null, "sku", SortDescending: true), ct);
        desc.Value.Items.Select(p => p.Sku).Should().ContainInOrder("C-3", "B-2", "A-1");
    }

    [Fact]
    public async Task Products_default_sort_is_by_name()
    {
        var ct = TestContext.Current.CancellationToken;
        Context.Products.AddRange(
            TestData.Product("B-2", "Apple"),
            TestData.Product("A-1", "Cherry"),
            TestData.Product("C-3", "Banana"));
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListProductsQueryHandler(queryContext);

        var result = await handler.Handle(new ListProductsQuery(null), ct);
        result.Value.Items.Select(p => p.Name).Should().ContainInOrder("Apple", "Banana", "Cherry");
    }

    [Fact]
    public async Task Locations_sort_by_code()
    {
        var ct = TestContext.Current.CancellationToken;
        Context.Locations.AddRange(
            TestData.Location("LOC-C", capacity: 30),
            TestData.Location("LOC-A", capacity: 10),
            TestData.Location("LOC-B", capacity: 20));
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListLocationsQueryHandler(queryContext);

        var byCode = await handler.Handle(new ListLocationsQuery(null, null, null, "code"), ct);
        byCode.Value.Items.Select(l => l.Code).Should().ContainInOrder("LOC-A", "LOC-B", "LOC-C");
    }

    [Fact]
    public async Task Locations_sort_by_capacity_uses_most_restrictive_occupancy_ratio()
    {
        var ct = TestContext.Current.CancellationToken;

        // A 2 kg product so weight occupancy is easy to reason about.
        var product = TestData.Product("CAP-SKU", "Heavy", weight: 2m, volume: 1m);
        Context.Products.Add(product);

        // A: units 10/100 = 10%.
        var a = TestData.Location("LOC-A", capacity: 100);
        // B: units 50/100 = 50%.
        var b = TestData.Location("LOC-B", capacity: 100);
        // C: weight only, 4 units * 2 kg = 8 kg / 10 kg = 80%.
        var c = TestData.Location("LOC-C", weightCapacity: 10m);
        // D: units 10/100 = 10% but weight 10 * 2 kg = 20 kg / 10 kg = 200%;
        //    the most restrictive (weight) wins, making D the fullest.
        var d = TestData.Location("LOC-D", capacity: 100, weightCapacity: 10m);
        Context.Locations.AddRange(a, b, c, d);

        Context.Inventories.AddRange(
            TestData.Inventory(product.Id, a.Id, onHand: 10),
            TestData.Inventory(product.Id, b.Id, onHand: 50),
            TestData.Inventory(product.Id, c.Id, onHand: 4),
            TestData.Inventory(product.Id, d.Id, onHand: 10));
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListLocationsQueryHandler(queryContext);

        var desc = await handler.Handle(
            new ListLocationsQuery(null, null, null, "capacity", SortDescending: true), ct);
        desc.Value.Items.Select(l => l.Code).Should().ContainInOrder("LOC-D", "LOC-C", "LOC-B", "LOC-A");

        var asc = await handler.Handle(new ListLocationsQuery(null, null, null, "capacity"), ct);
        asc.Value.Items.Select(l => l.Code).Should().ContainInOrder("LOC-A", "LOC-B", "LOC-C", "LOC-D");
    }

    [Fact]
    public async Task Locations_sort_by_address()
    {
        var ct = TestContext.Current.CancellationToken;
        Context.Locations.AddRange(
            TestData.LocationAt(new LocationAddress("Z2", "A1", "R1", "S1", "B1"), "LOC-1"),
            TestData.LocationAt(new LocationAddress("Z1", "A1", "R1", "S1", "B1"), "LOC-2"),
            TestData.LocationAt(new LocationAddress("Z1", "A2", "R1", "S1", "B1"), "LOC-3"));
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListLocationsQueryHandler(queryContext);

        var asc = await handler.Handle(new ListLocationsQuery(null, null, null, "address"), ct);
        asc.Value.Items.Select(l => l.Display)
            .Should().ContainInOrder("Z1-A1-R1-S1-B1", "Z1-A2-R1-S1-B1", "Z2-A1-R1-S1-B1");
    }

    [Fact]
    public async Task Lots_sort_by_number_product_and_expiration()
    {
        var ct = TestContext.Current.CancellationToken;
        var p1 = TestData.Product("AAA", "First");
        var p2 = TestData.Product("BBB", "Second");
        Context.Products.AddRange(p1, p2);
        Context.Lots.AddRange(
            TestData.Lot(p2.Id, "L2", new DateOnly(2030, 1, 1)),
            TestData.Lot(p1.Id, "L1", new DateOnly(2030, 6, 1)),
            TestData.Lot(p1.Id, "L3", new DateOnly(2030, 3, 1)));
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListLotsQueryHandler(queryContext);

        var byNumber = await handler.Handle(new ListLotsQuery(null, null, "number"), ct);
        byNumber.Value.Items.Select(l => l.Number).Should().ContainInOrder("L1", "L2", "L3");

        // Product sort keys off the product SKU (AAA before BBB), so both P1 lots come first.
        var byProduct = await handler.Handle(new ListLotsQuery(null, null, "product"), ct);
        byProduct.Value.Items.Take(2).Select(l => l.ProductId).Should().AllBeEquivalentTo(p1.Id);
        byProduct.Value.Items.Last().ProductId.Should().Be(p2.Id);

        var byExpiration = await handler.Handle(new ListLotsQuery(null, null, "expirationDate"), ct);
        byExpiration.Value.Items.Select(l => l.Number).Should().ContainInOrder("L2", "L3", "L1");
    }

    [Fact]
    public async Task Inventories_sort_by_on_hand_and_available()
    {
        var ct = TestContext.Current.CancellationToken;
        var product = TestData.Product("SKU-INV", "Inv");
        var loc = TestData.Location("INV-LOC");
        Context.Products.Add(product);
        Context.Locations.Add(loc);

        var low = TestData.Inventory(product.Id, loc.Id, onHand: 5);
        var high = TestData.Inventory(product.Id, loc.Id, onHand: 50);
        var mid = TestData.Inventory(product.Id, loc.Id, onHand: 20);
        Context.Inventories.AddRange(low, high, mid);
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListInventoriesQueryHandler(queryContext);

        var byOnHandDesc = await handler.Handle(
            new ListInventoriesQuery(null, null, null, SortBy: "onHand", SortDescending: true), ct);
        byOnHandDesc.Value.Items.Select(i => i.OnHand).Should().ContainInOrder(50, 20, 5);

        var byAvailableAsc = await handler.Handle(
            new ListInventoriesQuery(null, null, null, SortBy: "available"), ct);
        byAvailableAsc.Value.Items.Select(i => i.Available).Should().ContainInOrder(5, 20, 50);
    }

    [Fact]
    public async Task Inventories_sort_by_product_sku()
    {
        var ct = TestContext.Current.CancellationToken;
        var pB = TestData.Product("SKU-B", "Bee");
        var pA = TestData.Product("SKU-A", "Ant");
        var loc = TestData.Location("INV-LOC2");
        Context.Products.AddRange(pB, pA);
        Context.Locations.Add(loc);
        Context.Inventories.AddRange(
            TestData.Inventory(pB.Id, loc.Id, onHand: 1),
            TestData.Inventory(pA.Id, loc.Id, onHand: 1));
        await Context.SaveChangesAsync(ct);

        await using var queryContext = CreateContext();
        var handler = new ListInventoriesQueryHandler(queryContext);

        var byProduct = await handler.Handle(
            new ListInventoriesQuery(null, null, null, SortBy: "product"), ct);
        byProduct.Value.Items.Select(i => i.Product.Sku).Should().ContainInOrder("SKU-A", "SKU-B");
    }
}
