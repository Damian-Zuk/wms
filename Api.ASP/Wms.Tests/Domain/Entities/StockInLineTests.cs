using FluentAssertions;
using Wms.Domain.Entities;
using Wms.Domain.Enums;
using Wms.Domain.ValueObjects;
using Xunit;

namespace Wms.Tests.Domain.Entities;

public class StockInLineTests
{
    private static StockInLine NewLine(int quantity = 10) =>
        new(Guid.NewGuid(), null, new Quantity(quantity));

    [Fact]
    public void SetPlacements_succeeds_when_sum_matches()
    {
        var line = NewLine(10);

        var result = line.SetPlacements(
            [(Guid.NewGuid(), 6, PutawayStrategyType.NearestEmpty), (Guid.NewGuid(), 4, PutawayStrategyType.ConsolidateSameSku)]);

        result.IsSuccess.Should().BeTrue();
        line.Items.Should().HaveCount(2);
        line.PlacedTotal.Should().Be(10);
        line.Items.Select(i => i.Strategy).Should()
            .Contain([PutawayStrategyType.NearestEmpty, PutawayStrategyType.ConsolidateSameSku]);
    }

    [Fact]
    public void SetPlacements_rejects_sum_mismatch()
    {
        var line = NewLine(10);

        var result = line.SetPlacements([(Guid.NewGuid(), 6, PutawayStrategyType.NearestEmpty)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.PlacementsDoNotMatchLineTotal");
        line.Items.Should().BeEmpty();
    }

    [Fact]
    public void SetPlacements_rejects_empty()
    {
        var line = NewLine(10);

        var result = line.SetPlacements([]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.PlacementsRequired");
    }

    [Fact]
    public void SetPlacements_rejects_non_positive_quantity()
    {
        var line = NewLine(10);

        var result = line.SetPlacements(
            [(Guid.NewGuid(), 10, PutawayStrategyType.NearestEmpty), (Guid.NewGuid(), 0, PutawayStrategyType.NearestEmpty)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.PlacementQuantityMustBePositive");
    }

    [Fact]
    public void ReplacePlacementsManual_marks_all_manual_and_enforces_total()
    {
        var line = NewLine(10);
        line.SetPlacements([(Guid.NewGuid(), 10, PutawayStrategyType.NearestEmpty)]);

        var result = line.ReplacePlacementsManual([(Guid.NewGuid(), 4), (Guid.NewGuid(), 6)]);

        result.IsSuccess.Should().BeTrue();
        line.Items.Should().HaveCount(2);
        line.Items.Should().OnlyContain(i => i.Strategy == PutawayStrategyType.Manual);
        line.PlacedTotal.Should().Be(10);
    }

    [Fact]
    public void ReplacePlacementsManual_rejects_sum_mismatch()
    {
        var line = NewLine(10);
        line.SetPlacements([(Guid.NewGuid(), 10, PutawayStrategyType.NearestEmpty)]);

        var result = line.ReplacePlacementsManual([(Guid.NewGuid(), 4)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StockIn.PlacementsDoNotMatchLineTotal");
    }
}
