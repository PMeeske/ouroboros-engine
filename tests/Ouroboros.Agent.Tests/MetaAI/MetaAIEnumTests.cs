using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;
using MetaAgentStatus = Ouroboros.Agent.MetaAI.AgentStatus;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MetaAIEnumTests
{
    [Theory]
    [InlineData(MetaAgentStatus.Available)]
    [InlineData(MetaAgentStatus.Busy)]
    [InlineData(MetaAgentStatus.Offline)]
    public void AgentStatus_AllValues_AreDefined(MetaAgentStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void AgentStatus_HasThreeValues()
    {
        Enum.GetValues<MetaAgentStatus>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(ExplanationLevel.Brief)]
    [InlineData(ExplanationLevel.Detailed)]
    [InlineData(ExplanationLevel.Causal)]
    [InlineData(ExplanationLevel.Counterfactual)]
    public void ExplanationLevel_AllValues_AreDefined(ExplanationLevel level)
    {
        Enum.IsDefined(level).Should().BeTrue();
    }

    [Fact]
    public void ExplanationLevel_HasFourValues()
    {
        Enum.GetValues<ExplanationLevel>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(ReviewStatus.Draft)]
    [InlineData(ReviewStatus.AwaitingReview)]
    [InlineData(ReviewStatus.ChangesRequested)]
    [InlineData(ReviewStatus.Approved)]
    [InlineData(ReviewStatus.Merged)]
    public void ReviewStatus_AllValues_AreDefined(ReviewStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void ReviewStatus_HasFiveValues()
    {
        Enum.GetValues<ReviewStatus>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(ReviewCommentStatus.Open)]
    [InlineData(ReviewCommentStatus.Resolved)]
    [InlineData(ReviewCommentStatus.Dismissed)]
    public void ReviewCommentStatus_AllValues_AreDefined(ReviewCommentStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void ReviewCommentStatus_HasThreeValues()
    {
        Enum.GetValues<ReviewCommentStatus>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(ImprovementPhase.Plan, 1)]
    [InlineData(ImprovementPhase.Execute, 2)]
    [InlineData(ImprovementPhase.Verify, 3)]
    [InlineData(ImprovementPhase.Learn, 4)]
    public void ImprovementPhase_ValuesAreOrdered(ImprovementPhase phase, int expected)
    {
        ((int)phase).Should().Be(expected);
    }

    [Theory]
    [InlineData(RepairStrategy.Replan)]
    [InlineData(RepairStrategy.Patch)]
    [InlineData(RepairStrategy.CaseBased)]
    [InlineData(RepairStrategy.Backtrack)]
    public void RepairStrategy_AllValues_AreDefined(RepairStrategy strategy)
    {
        Enum.IsDefined(strategy).Should().BeTrue();
    }

    [Fact]
    public void RepairStrategy_HasFourValues()
    {
        Enum.GetValues<RepairStrategy>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(CostOptimizationStrategy.MinimizeCost)]
    [InlineData(CostOptimizationStrategy.MaximizeQuality)]
    [InlineData(CostOptimizationStrategy.Balanced)]
    [InlineData(CostOptimizationStrategy.MaximizeValue)]
    public void CostOptimizationStrategy_AllValues_AreDefined(CostOptimizationStrategy strategy)
    {
        Enum.IsDefined(strategy).Should().BeTrue();
    }

    [Fact]
    public void CostOptimizationStrategy_HasFourValues()
    {
        Enum.GetValues<CostOptimizationStrategy>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(ExperimentStatus.Running)]
    [InlineData(ExperimentStatus.Completed)]
    [InlineData(ExperimentStatus.Cancelled)]
    [InlineData(ExperimentStatus.Failed)]
    public void ExperimentStatus_AllValues_AreDefined(ExperimentStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void ExperimentStatus_HasFourValues()
    {
        Enum.GetValues<ExperimentStatus>().Should().HaveCount(4);
    }
}
