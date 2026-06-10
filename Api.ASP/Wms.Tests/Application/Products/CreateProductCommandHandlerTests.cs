using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.Products.Commands;
using Wms.Domain.Enums;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Products;

public class CreateProductCommandHandlerTests : IntegrationTestBase
{
    public CreateProductCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Happy_path_creates_product_and_returns_id()
    {
        var handler = new CreateProductCommandHandler(Context);
        var command = new CreateProductCommand(
            Sku: "SKU-001",
            Name: "Widget",
            Description: "a widget",
            Weight: 2.5m,
            Volume: 1.5m,
            UnitPrice: 3.75m,
            RequiredTemperatureZone: TemperatureZone.Chilled);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var ct = TestContext.Current.CancellationToken;

        await using var verify = CreateContext();
        var stored = await verify.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == result.Value, ct);

        stored.Sku.Value.Should().Be("SKU-001");
        stored.Name.Should().Be("Widget");
        stored.Weight.Should().Be(2.5m);
        stored.Volume.Should().Be(1.5m);
        stored.UnitPrice.Should().Be(3.75m);
        stored.RequiredTemperatureZone.Should().Be(TemperatureZone.Chilled);
    }

    [Fact]
    public async Task Duplicate_sku_is_rejected()
    {
        var handler = new CreateProductCommandHandler(Context);

        var first = await handler.Handle(
            new CreateProductCommand("SKU-DUP", "First", "first", 1m, 1m, 1m),
            TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();

        // Use a fresh context for the second call so the handler hits the DB
        // and doesn't just see a tracked entity from the first call.
        await using var secondContext = CreateContext();
        var second = await new CreateProductCommandHandler(secondContext).Handle(
            new CreateProductCommand("SKU-DUP", "Second", "second", 1m, 1m, 1m),
            TestContext.Current.CancellationToken);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Product.SkuExists");

        var ct = TestContext.Current.CancellationToken;

        await using var verify = CreateContext();
        var count = await verify.Products
            .AsNoTracking()
            .CountAsync(p => p.Sku.Value == "SKU-DUP", ct);
        count.Should().Be(1);
    }

    public class PreferredLocations : CreateProductCommandHandlerTests
    {
        public PreferredLocations(PostgresContainerFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Non_existent_location_is_rejected()
        {
            var realLocation = TestData.Location("PL-EXIST");
            Context.Locations.Add(realLocation);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateProductCommandHandler(actContext);

            var missing = Guid.NewGuid();
            var result = await handler.Handle(
                new CreateProductCommand(
                    "SKU-PL-1",
                    "Widget",
                    "desc",
                    1m,
                    1m,
                    1m,
                    PreferredLocationIds: new[] { realLocation.Id, missing }),
                TestContext.Current.CancellationToken);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Location.NotFound");

            await using var verify = CreateContext();
            var count = await verify.Products
                .AsNoTracking()
                .CountAsync(p => p.Sku.Value == "SKU-PL-1", TestContext.Current.CancellationToken);
            count.Should().Be(0);
        }

        [Fact]
        public async Task Duplicates_and_empty_guids_are_collapsed_with_caller_order_preserved()
        {
            var locA = TestData.Location("PL-A");
            var locB = TestData.Location("PL-B");
            Context.Locations.AddRange(locA, locB);
            await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var actContext = CreateContext();
            var handler = new CreateProductCommandHandler(actContext);

            // Caller passes B, empty, A, A, B — handler should keep first
            // occurrence of B then A, drop empties and duplicates.
            var result = await handler.Handle(
                new CreateProductCommand(
                    "SKU-PL-2",
                    "Widget",
                    "desc",
                    1m,
                    1m,
                    1m,
                    PreferredLocationIds: new[]
                    {
                        locB.Id, Guid.Empty, locA.Id, locA.Id, locB.Id
                    }),
                TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();

            await using var verify = CreateContext();
            var stored = await verify.Products
                .AsNoTracking()
                .Include(p => p.PreferredLocations)
                .SingleAsync(p => p.Id == result.Value, TestContext.Current.CancellationToken);

            stored.PreferredLocations
                .OrderBy(p => p.Sequence)
                .Select(p => p.LocationId)
                .Should().Equal(locB.Id, locA.Id);
        }

        [Fact]
        public async Task Null_collection_persists_no_preferred_locations()
        {
            await using var actContext = CreateContext();
            var handler = new CreateProductCommandHandler(actContext);

            var result = await handler.Handle(
                new CreateProductCommand("SKU-PL-3", "Widget", "desc", 1m, 1m, 1m, PreferredLocationIds: null),
                TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();

            await using var verify = CreateContext();
            var stored = await verify.Products
                .AsNoTracking()
                .Include(p => p.PreferredLocations)
                .SingleAsync(p => p.Id == result.Value, TestContext.Current.CancellationToken);
            stored.PreferredLocations.Should().BeEmpty();
        }
    }
}
