using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Handlers.HandlingUnits.Commands;
using Wms.Domain.Enums;
using Wms.Infrastructure.HandlingUnits;
using Wms.Tests.Common;
using Xunit;

namespace Wms.Tests.Application.HandlingUnits;

public class CreateHandlingUnitCommandHandlerTests : IntegrationTestBase
{
    public CreateHandlingUnitCommandHandlerTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Creates_a_placed_unit_with_a_generated_code()
    {
        var ct = TestContext.Current.CancellationToken;

        var location = TestData.Location("HU-CR-1");
        Context.Locations.Add(location);
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateHandlingUnitCommandHandler(actContext, new HandlingUnitCodeGenerator(actContext));

        var first = await handler.Handle(
            new CreateHandlingUnitCommand(location.Id, HandlingUnitType.Pallet), ct);
        var second = await handler.Handle(
            new CreateHandlingUnitCommand(location.Id, HandlingUnitType.Box), ct);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var units = await verify.HandlingUnits.AsNoTracking().ToListAsync(ct);
        units.Should().HaveCount(2);
        units.Should().OnlyContain(h => h.LocationId == location.Id);
        units.Should().OnlyContain(h => h.Code.Value.StartsWith("HU-"));
        units.Select(h => h.Code.Value).Distinct().Should().HaveCount(2, "the sequence hands out distinct numbers");
    }

    [Fact]
    public async Task Manual_code_is_kept_and_duplicates_are_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var location = TestData.Location("HU-CR-2");
        Context.Locations.Add(location);
        Context.HandlingUnits.Add(TestData.HandlingUnit("HU-CR-2-TAKEN"));
        await Context.SaveChangesAsync(ct);

        await using var actContext = CreateContext();
        var handler = new CreateHandlingUnitCommandHandler(actContext, new HandlingUnitCodeGenerator(actContext));

        var ok = await handler.Handle(
            new CreateHandlingUnitCommand(location.Id, HandlingUnitType.Container, "HU-CR-2-MINE"), ct);
        ok.IsSuccess.Should().BeTrue();

        var duplicate = await handler.Handle(
            new CreateHandlingUnitCommand(location.Id, HandlingUnitType.Container, "HU-CR-2-TAKEN"), ct);
        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Code.Should().Be("HandlingUnit.CodeAlreadyExists");
    }

    [Fact]
    public async Task Unknown_location_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var actContext = CreateContext();
        var handler = new CreateHandlingUnitCommandHandler(actContext, new HandlingUnitCodeGenerator(actContext));

        var result = await handler.Handle(
            new CreateHandlingUnitCommand(Guid.NewGuid(), HandlingUnitType.Pallet), ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HandlingUnit.LocationNotFound");
    }
}
