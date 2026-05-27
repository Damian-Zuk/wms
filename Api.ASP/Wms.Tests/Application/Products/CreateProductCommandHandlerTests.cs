using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Features.Products.Commands;
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
        stored.RequiredTemperatureZone.Should().Be(TemperatureZone.Chilled);
    }

    [Fact]
    public async Task Duplicate_sku_is_rejected()
    {
        var handler = new CreateProductCommandHandler(Context);

        var first = await handler.Handle(
            new CreateProductCommand("SKU-DUP", "First", "first"),
            TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();

        // Use a fresh context for the second call so the handler hits the DB
        // and doesn't just see a tracked entity from the first call.
        await using var secondContext = CreateContext();
        var second = await new CreateProductCommandHandler(secondContext).Handle(
            new CreateProductCommand("SKU-DUP", "Second", "second"),
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
}
