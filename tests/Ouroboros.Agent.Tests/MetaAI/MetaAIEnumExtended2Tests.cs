using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ReviewStatusTests
{
    [Theory]
    [InlineData(ReviewStatus.Draft)]
    [InlineData(ReviewStatus.AwaitingReview)]
    [InlineData(ReviewStatus.ChangesRequested)]
    [InlineData(ReviewStatus.Approved)]
    [InlineData(ReviewStatus.Merged)]
    public void AllValues_AreDefined(ReviewStatus value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void ReviewStatus_HasFiveValues()
    {
        Enum.GetValues<ReviewStatus>().Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public class CostOptimizationStrategyTests
{
    [Theory]
    [InlineData(CostOptimizationStrategy.MinimizeCost)]
    [InlineData(CostOptimizationStrategy.MaximizeQuality)]
    [InlineData(CostOptimizationStrategy.Balanced)]
    [InlineData(CostOptimizationStrategy.MaximizeValue)]
    public void AllValues_AreDefined(CostOptimizationStrategy value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void CostOptimizationStrategy_HasFourValues()
    {
        Enum.GetValues<CostOptimizationStrategy>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class ExplanationLevelTests
{
    [Theory]
    [InlineData(ExplanationLevel.Brief)]
    [InlineData(ExplanationLevel.Detailed)]
    [InlineData(ExplanationLevel.Causal)]
    [InlineData(ExplanationLevel.Counterfactual)]
    public void AllValues_AreDefined(ExplanationLevel value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void ExplanationLevel_HasFourValues()
    {
        Enum.GetValues<ExplanationLevel>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class ImprovementPhaseTests
{
    [Theory]
    [InlineData(ImprovementPhase.Plan, 1)]
    [InlineData(ImprovementPhase.Execute, 2)]
    [InlineData(ImprovementPhase.Verify, 3)]
    [InlineData(ImprovementPhase.Learn, 4)]
    public void AllValues_HaveExpectedNumericValue(ImprovementPhase value, int expected)
    {
        ((int)value).Should().Be(expected);
    }

    [Fact]
    public void ImprovementPhase_HasFourValues()
    {
        Enum.GetValues<ImprovementPhase>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class RepairStrategyTests
{
    [Theory]
    [InlineData(RepairStrategy.Replan)]
    [InlineData(RepairStrategy.Patch)]
    [InlineData(RepairStrategy.CaseBased)]
    [InlineData(RepairStrategy.Backtrack)]
    public void AllValues_AreDefined(RepairStrategy value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void RepairStrategy_HasFourValues()
    {
        Enum.GetValues<RepairStrategy>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class ExperimentStatusTests
{
    [Theory]
    [InlineData(ExperimentStatus.Running)]
    [InlineData(ExperimentStatus.Completed)]
    [InlineData(ExperimentStatus.Cancelled)]
    [InlineData(ExperimentStatus.Failed)]
    public void AllValues_AreDefined(ExperimentStatus value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void ExperimentStatus_HasFourValues()
    {
        Enum.GetValues<ExperimentStatus>().Should().HaveCount(4);
    }
}
