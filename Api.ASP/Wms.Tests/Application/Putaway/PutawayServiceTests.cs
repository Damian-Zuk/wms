using FluentAssertions;
using Wms.Application.Putaway;
using Wms.Domain.Entities;
using Wms.Domain.ValueObjects;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.Putaway;

/// <summary>
/// PutawayService is the chain runner. Strategies are stubbed with
/// FakeStrategy so these tests focus purely on the chain semantics
/// (order, fall-through, terminal failure) — the strategies themselves
/// are covered by their own per-strategy test files.
/// </summary>
public class PutawayServiceTests : IntegrationTestBase
{
    public PutawayServiceTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Fixed_wins_when_the_first_strategy_returns_a_suggestion()
    {
        var product = await SeedProductAsync("PS-1");
        var fixedLocId = Guid.NewGuid();

        var service = new PutawayService(Context, new IPutawayStrategy[]
        {
            new FakeStrategy("FixedLocation", new PutawaySuggestion(fixedLocId, "FixedLocation")),
            new FakeStrategy("ConsolidateSameSku", FailIfCalled()),
            new FakeStrategy("NearestEmpty", FailIfCalled()),
        });

        var result = await service.SuggestAsync(
            product.Id, lotId: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.LocationId.Should().Be(fixedLocId);
        result.Value.StrategyName.Should().Be("FixedLocation");
    }

    [Fact]
    public async Task Falls_through_to_consolidate_when_fixed_returns_null()
    {
        var product = await SeedProductAsync("PS-2");
        var consolLocId = Guid.NewGuid();

        var service = new PutawayService(Context, new IPutawayStrategy[]
        {
            new FakeStrategy("FixedLocation", suggestion: null),
            new FakeStrategy("ConsolidateSameSku", new PutawaySuggestion(consolLocId, "ConsolidateSameSku")),
            new FakeStrategy("NearestEmpty", FailIfCalled()),
        });

        var result = await service.SuggestAsync(
            product.Id, lotId: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.LocationId.Should().Be(consolLocId);
        result.Value.StrategyName.Should().Be("ConsolidateSameSku");
    }

    [Fact]
    public async Task Falls_through_to_nearest_when_fixed_and_consolidate_return_null()
    {
        var product = await SeedProductAsync("PS-3");
        var nearestLocId = Guid.NewGuid();

        var service = new PutawayService(Context, new IPutawayStrategy[]
        {
            new FakeStrategy("FixedLocation", suggestion: null),
            new FakeStrategy("ConsolidateSameSku", suggestion: null),
            new FakeStrategy("NearestEmpty", new PutawaySuggestion(nearestLocId, "NearestEmpty")),
        });

        var result = await service.SuggestAsync(
            product.Id, lotId: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.LocationId.Should().Be(nearestLocId);
        result.Value.StrategyName.Should().Be("NearestEmpty");
    }

    [Fact]
    public async Task Returns_no_suitable_location_when_every_strategy_returns_null()
    {
        var product = await SeedProductAsync("PS-4");

        var service = new PutawayService(Context, new IPutawayStrategy[]
        {
            new FakeStrategy("FixedLocation", null),
            new FakeStrategy("ConsolidateSameSku", null),
            new FakeStrategy("NearestEmpty", null),
        });

        var result = await service.SuggestAsync(
            product.Id, lotId: null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Putaway.NoSuitableLocation");
    }

    [Fact]
    public async Task Strategies_are_invoked_in_the_registered_order()
    {
        // Each strategy records its name when called. The first non-null
        // wins, and no strategy after it should run.
        var product = await SeedProductAsync("PS-5");
        var calls = new List<string>();

        var winningSuggestion = new PutawaySuggestion(Guid.NewGuid(), "B");

        var service = new PutawayService(Context, new IPutawayStrategy[]
        {
            new RecordingStrategy("A", suggestion: null, calls),
            new RecordingStrategy("B", winningSuggestion, calls),
            new RecordingStrategy("C", suggestion: null, calls),
        });

        var result = await service.SuggestAsync(
            product.Id, null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.StrategyName.Should().Be("B");
        calls.Should().Equal("A", "B");
    }

    [Fact]
    public async Task Missing_product_returns_product_not_found_without_invoking_strategies()
    {
        var calls = new List<string>();
        var service = new PutawayService(Context, new IPutawayStrategy[]
        {
            new RecordingStrategy("FixedLocation", null, calls),
        });

        var result = await service.SuggestAsync(
            Guid.NewGuid(), null, new Quantity(1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Putaway.ProductNotFound");
        calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_lot_returns_lot_not_found_without_invoking_strategies()
    {
        var product = await SeedProductAsync("PS-6");

        var calls = new List<string>();
        var service = new PutawayService(Context, new IPutawayStrategy[]
        {
            new RecordingStrategy("FixedLocation", null, calls),
        });

        var result = await service.SuggestAsync(
            product.Id, lotId: Guid.NewGuid(), new Quantity(1),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Putaway.LotNotFound");
        calls.Should().BeEmpty();
    }

    private async Task<Product> SeedProductAsync(string sku)
    {
        var product = TestData.Product(sku);
        Context.Products.Add(product);
        await Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product;
    }

    private static PutawaySuggestion FailIfCalled() =>
        // Sentinel: the test marks "this strategy must NOT be called". Using
        // it directly would also be a valid suggestion, but the test relies
        // on the earlier strategy having already won.
        new(Guid.Empty, "SHOULD_NOT_BE_CALLED");

    private sealed class FakeStrategy(string name, PutawaySuggestion? suggestion) : IPutawayStrategy
    {
        public string Name => name;

        public Task<PutawaySuggestion?> SuggestAsync(
            Product product, Lot? lot, Quantity quantity, CancellationToken ct) =>
            Task.FromResult(suggestion);
    }

    private sealed class RecordingStrategy(string name, PutawaySuggestion? suggestion, List<string> calls) : IPutawayStrategy
    {
        public string Name => name;

        public Task<PutawaySuggestion?> SuggestAsync(
            Product product, Lot? lot, Quantity quantity, CancellationToken ct)
        {
            calls.Add(name);
            return Task.FromResult(suggestion);
        }
    }
}
