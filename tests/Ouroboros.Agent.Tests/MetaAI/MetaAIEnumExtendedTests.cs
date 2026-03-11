using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AdaptationStrategyTests
{
    [Theory]
    [InlineData(AdaptationStrategy.Retry)]
    [InlineData(AdaptationStrategy.ReplaceStep)]
    [InlineData(AdaptationStrategy.AddStep)]
    [InlineData(AdaptationStrategy.Replan)]
    [InlineData(AdaptationStrategy.Abort)]
    public void AllValues_AreDefined(AdaptationStrategy strategy)
    {
        Enum.IsDefined(strategy).Should().BeTrue();
    }

    [Fact]
    public void HasFiveValues()
    {
        Enum.GetValues<AdaptationStrategy>().Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public class SubIssueStatusTests
{
    [Theory]
    [InlineData(SubIssueStatus.Pending)]
    [InlineData(SubIssueStatus.BranchCreated)]
    [InlineData(SubIssueStatus.InProgress)]
    [InlineData(SubIssueStatus.Completed)]
    [InlineData(SubIssueStatus.Failed)]
    public void AllValues_AreDefined(SubIssueStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void HasFiveValues()
    {
        Enum.GetValues<SubIssueStatus>().Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public class TemporalRelationTests
{
    [Theory]
    [InlineData(TemporalRelation.Before)]
    [InlineData(TemporalRelation.After)]
    [InlineData(TemporalRelation.During)]
    [InlineData(TemporalRelation.Overlaps)]
    [InlineData(TemporalRelation.MustFinishBefore)]
    [InlineData(TemporalRelation.Simultaneous)]
    public void AllValues_AreDefined(TemporalRelation relation)
    {
        Enum.IsDefined(relation).Should().BeTrue();
    }

    [Fact]
    public void HasSixValues()
    {
        Enum.GetValues<TemporalRelation>().Should().HaveCount(6);
    }
}

[Trait("Category", "Unit")]
public class ToolCategoryTests
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
    public void AllValues_AreDefined(ToolCategory category)
    {
        Enum.IsDefined(category).Should().BeTrue();
    }

    [Fact]
    public void HasElevenValues()
    {
        Enum.GetValues<ToolCategory>().Should().HaveCount(11);
    }
}

[Trait("Category", "Unit")]
public class SafetyConstraintsTests
{
    [Fact]
    public void None_HasValueZero()
    {
        ((int)SafetyConstraints.None).Should().Be(0);
    }

    [Fact]
    public void All_CombinesAllFlags()
    {
        SafetyConstraints expected = SafetyConstraints.NoSelfDestruction
            | SafetyConstraints.PreserveHumanOversight
            | SafetyConstraints.BoundedResourceUse
            | SafetyConstraints.ReversibleActions;

        SafetyConstraints.All.Should().Be(expected);
    }

    [Theory]
    [InlineData(SafetyConstraints.NoSelfDestruction, 1)]
    [InlineData(SafetyConstraints.PreserveHumanOversight, 2)]
    [InlineData(SafetyConstraints.BoundedResourceUse, 4)]
    [InlineData(SafetyConstraints.ReversibleActions, 8)]
    public void FlagValues_ArePowersOfTwo(SafetyConstraints constraint, int expected)
    {
        ((int)constraint).Should().Be(expected);
    }

    [Fact]
    public void Flags_CanBeCombined()
    {
        SafetyConstraints combined = SafetyConstraints.NoSelfDestruction | SafetyConstraints.ReversibleActions;
        combined.HasFlag(SafetyConstraints.NoSelfDestruction).Should().BeTrue();
        combined.HasFlag(SafetyConstraints.ReversibleActions).Should().BeTrue();
        combined.HasFlag(SafetyConstraints.PreserveHumanOversight).Should().BeFalse();
    }

    [Fact]
    public void All_ContainsEveryFlag()
    {
        SafetyConstraints.All.HasFlag(SafetyConstraints.NoSelfDestruction).Should().BeTrue();
        SafetyConstraints.All.HasFlag(SafetyConstraints.PreserveHumanOversight).Should().BeTrue();
        SafetyConstraints.All.HasFlag(SafetyConstraints.BoundedResourceUse).Should().BeTrue();
        SafetyConstraints.All.HasFlag(SafetyConstraints.ReversibleActions).Should().BeTrue();
    }

    [Fact]
    public void HasFiveDefinedValues()
    {
        Enum.GetValues<SafetyConstraints>().Should().HaveCount(6);
    }
}

[Trait("Category", "Unit")]
public class OuroborosConfidenceTests
{
    [Theory]
    [InlineData(OuroborosConfidence.High)]
    [InlineData(OuroborosConfidence.Medium)]
    [InlineData(OuroborosConfidence.Low)]
    public void AllValues_AreDefined(OuroborosConfidence confidence)
    {
        Enum.IsDefined(confidence).Should().BeTrue();
    }

    [Fact]
    public void HasThreeValues()
    {
        Enum.GetValues<OuroborosConfidence>().Should().HaveCount(3);
    }
}

[Trait("Category", "Unit")]
public class EvidenceStrengthTests
{
    [Theory]
    [InlineData(EvidenceStrength.Negligible)]
    [InlineData(EvidenceStrength.Substantial)]
    [InlineData(EvidenceStrength.Strong)]
    [InlineData(EvidenceStrength.VeryStrong)]
    [InlineData(EvidenceStrength.Decisive)]
    public void AllValues_AreDefined(EvidenceStrength strength)
    {
        Enum.IsDefined(strength).Should().BeTrue();
    }

    [Fact]
    public void HasFiveValues()
    {
        Enum.GetValues<EvidenceStrength>().Should().HaveCount(5);
    }

    [Fact]
    public void ValuesAreOrdered()
    {
        ((int)EvidenceStrength.Negligible).Should().BeLessThan((int)EvidenceStrength.Substantial);
        ((int)EvidenceStrength.Substantial).Should().BeLessThan((int)EvidenceStrength.Strong);
        ((int)EvidenceStrength.Strong).Should().BeLessThan((int)EvidenceStrength.VeryStrong);
        ((int)EvidenceStrength.VeryStrong).Should().BeLessThan((int)EvidenceStrength.Decisive);
    }
}

[Trait("Category", "Unit")]
public class GoalTypeTests
{
    [Theory]
    [InlineData(GoalType.Primary)]
    [InlineData(GoalType.Secondary)]
    [InlineData(GoalType.Instrumental)]
    [InlineData(GoalType.Safety)]
    public void AllValues_AreDefined(GoalType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Fact]
    public void HasFourValues()
    {
        Enum.GetValues<GoalType>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class MemoryTypeTests
{
    [Theory]
    [InlineData(MemoryType.Episodic)]
    [InlineData(MemoryType.Semantic)]
    public void AllValues_AreDefined(MemoryType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Fact]
    public void HasTwoValues()
    {
        Enum.GetValues<MemoryType>().Should().HaveCount(2);
    }
}
