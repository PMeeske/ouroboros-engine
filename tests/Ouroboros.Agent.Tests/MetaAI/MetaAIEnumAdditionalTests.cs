using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AdaptationStrategyEnumTests
{
    [Theory]
    [InlineData(AdaptationStrategy.Retry)]
    [InlineData(AdaptationStrategy.ReplaceStep)]
    [InlineData(AdaptationStrategy.AddStep)]
    [InlineData(AdaptationStrategy.Replan)]
    [InlineData(AdaptationStrategy.Abort)]
    public void AdaptationStrategy_AllValues_AreDefined(AdaptationStrategy strategy)
    {
        Enum.IsDefined(strategy).Should().BeTrue();
    }

    [Fact]
    public void AdaptationStrategy_HasFiveValues()
    {
        Enum.GetValues<AdaptationStrategy>().Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public class SafetyConstraintsEnumTests
{
    [Fact]
    public void SafetyConstraints_None_ShouldBeZero()
    {
        ((int)SafetyConstraints.None).Should().Be(0);
    }

    [Fact]
    public void SafetyConstraints_IndividualFlags_ShouldHaveCorrectValues()
    {
        ((int)SafetyConstraints.NoSelfDestruction).Should().Be(1);
        ((int)SafetyConstraints.PreserveHumanOversight).Should().Be(2);
        ((int)SafetyConstraints.BoundedResourceUse).Should().Be(4);
        ((int)SafetyConstraints.ReversibleActions).Should().Be(8);
    }

    [Fact]
    public void SafetyConstraints_All_ShouldCombineAllFlags()
    {
        var all = SafetyConstraints.All;
        all.HasFlag(SafetyConstraints.NoSelfDestruction).Should().BeTrue();
        all.HasFlag(SafetyConstraints.PreserveHumanOversight).Should().BeTrue();
        all.HasFlag(SafetyConstraints.BoundedResourceUse).Should().BeTrue();
        all.HasFlag(SafetyConstraints.ReversibleActions).Should().BeTrue();
    }

    [Fact]
    public void SafetyConstraints_All_ShouldEqual15()
    {
        ((int)SafetyConstraints.All).Should().Be(15);
    }

    [Fact]
    public void SafetyConstraints_CanCombineFlags()
    {
        var combined = SafetyConstraints.NoSelfDestruction | SafetyConstraints.PreserveHumanOversight;
        combined.HasFlag(SafetyConstraints.NoSelfDestruction).Should().BeTrue();
        combined.HasFlag(SafetyConstraints.PreserveHumanOversight).Should().BeTrue();
        combined.HasFlag(SafetyConstraints.BoundedResourceUse).Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class SubIssueStatusEnumTests
{
    [Theory]
    [InlineData(SubIssueStatus.Pending)]
    [InlineData(SubIssueStatus.BranchCreated)]
    [InlineData(SubIssueStatus.InProgress)]
    [InlineData(SubIssueStatus.Completed)]
    [InlineData(SubIssueStatus.Failed)]
    public void SubIssueStatus_AllValues_AreDefined(SubIssueStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void SubIssueStatus_HasFiveValues()
    {
        Enum.GetValues<SubIssueStatus>().Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public class TaskAssignmentStatusEnumTests
{
    [Theory]
    [InlineData(TaskAssignmentStatus.Pending)]
    [InlineData(TaskAssignmentStatus.InProgress)]
    [InlineData(TaskAssignmentStatus.Completed)]
    [InlineData(TaskAssignmentStatus.Failed)]
    public void TaskAssignmentStatus_AllValues_AreDefined(TaskAssignmentStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void TaskAssignmentStatus_HasFourValues()
    {
        Enum.GetValues<TaskAssignmentStatus>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class TemporalRelationEnumTests
{
    [Theory]
    [InlineData(TemporalRelation.Before)]
    [InlineData(TemporalRelation.After)]
    [InlineData(TemporalRelation.During)]
    [InlineData(TemporalRelation.Overlaps)]
    [InlineData(TemporalRelation.MustFinishBefore)]
    [InlineData(TemporalRelation.Simultaneous)]
    public void TemporalRelation_AllValues_AreDefined(TemporalRelation relation)
    {
        Enum.IsDefined(relation).Should().BeTrue();
    }

    [Fact]
    public void TemporalRelation_HasSixValues()
    {
        Enum.GetValues<TemporalRelation>().Should().HaveCount(6);
    }
}

[Trait("Category", "Unit")]
public class ToolCategoryEnumTests
{
    [Theory]
    [InlineData(ToolCategory.General)]
    [InlineData(ToolCategory.Code)]
    [InlineData(ToolCategory.FileSystem)]
    [InlineData(ToolCategory.Web)]
    [InlineData(ToolCategory.Knowledge)]
    [InlineData(ToolCategory.Analysis)]
    [InlineData(ToolCategory.Validation)]
    [InlineData(ToolCategory.Text)]
    [InlineData(ToolCategory.Reasoning)]
    [InlineData(ToolCategory.Creative)]
    [InlineData(ToolCategory.Utility)]
    public void ToolCategory_AllValues_AreDefined(ToolCategory category)
    {
        Enum.IsDefined(category).Should().BeTrue();
    }

    [Fact]
    public void ToolCategory_HasElevenValues()
    {
        Enum.GetValues<ToolCategory>().Should().HaveCount(11);
    }
}

[Trait("Category", "Unit")]
public class OuroborosConfidenceEnumTests
{
    [Theory]
    [InlineData(OuroborosConfidence.High)]
    [InlineData(OuroborosConfidence.Medium)]
    [InlineData(OuroborosConfidence.Low)]
    public void OuroborosConfidence_AllValues_AreDefined(OuroborosConfidence confidence)
    {
        Enum.IsDefined(confidence).Should().BeTrue();
    }

    [Fact]
    public void OuroborosConfidence_HasThreeValues()
    {
        Enum.GetValues<OuroborosConfidence>().Should().HaveCount(3);
    }
}
