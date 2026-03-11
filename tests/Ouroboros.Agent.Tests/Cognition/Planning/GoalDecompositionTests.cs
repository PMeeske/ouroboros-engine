using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.Cognition.Planning;

[Trait("Category", "Unit")]
public class GoalStepTests
{
    [Fact]
    public void ShortConstructor_SetsDefaultsCorrectly()
    {
        var coord = new DimensionalCoordinate(0.5, 0.5, 0.5, 0.5);

        var step = new GoalStep("Analyze data", GoalType.Analysis, 0.8, coord);

        step.Description.Should().Be("Analyze data");
        step.Type.Should().Be(GoalType.Analysis);
        step.Priority.Should().Be(0.8);
        step.Coordinate.Should().Be(coord);
        step.DependsOn.Should().BeEmpty();
        step.Mode.Should().Be(ExecutionMode.Automatic);
        step.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameId_AreEqual()
    {
        var id = Guid.NewGuid();
        var coord = DimensionalCoordinate.Origin;

        var a = new GoalStep(id, "desc", GoalType.Analysis, 0.5, coord, Array.Empty<Guid>(), ExecutionMode.Automatic);
        var b = new GoalStep(id, "desc", GoalType.Analysis, 0.5, coord, Array.Empty<Guid>(), ExecutionMode.Automatic);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class ExecutionModeTests
{
    [Theory]
    [InlineData(ExecutionMode.Automatic)]
    [InlineData(ExecutionMode.RequiresApproval)]
    [InlineData(ExecutionMode.ToolDelegation)]
    [InlineData(ExecutionMode.HumanDelegation)]
    public void AllValues_AreDefined(ExecutionMode value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void ExecutionMode_HasFourValues()
    {
        Enum.GetValues<ExecutionMode>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class ModificationEventTests
{
    [Fact]
    public void ModificationFailedEvent_SetsProperties()
    {
        var id = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var evt = new ModificationFailedEvent(id, timestamp, proposalId, "Something went wrong");

        evt.Id.Should().Be(id);
        evt.Timestamp.Should().Be(timestamp);
        evt.ProposalId.Should().Be(proposalId);
        evt.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public void ModificationRolledBackEvent_SetsProperties()
    {
        var id = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var evt = new ModificationRolledBackEvent(id, timestamp, proposalId, snapshotId, "Safety violation");

        evt.ProposalId.Should().Be(proposalId);
        evt.SnapshotId.Should().Be(snapshotId);
        evt.Reason.Should().Be("Safety violation");
    }

    [Fact]
    public void ModificationProposedEvent_SetsEventType()
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var evt = new ModificationProposedEvent(id, timestamp, null!);

        evt.EventType.Should().Be("ModificationProposed");
    }

    [Fact]
    public void ModificationDecidedEvent_SetsEventType()
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var evt = new ModificationDecidedEvent(id, timestamp, null!);

        evt.EventType.Should().Be("ModificationDecided");
    }

    [Fact]
    public void ModificationExecutedEvent_SetsEventType()
    {
        var id = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var evt = new ModificationExecutedEvent(id, timestamp, proposalId, null!);

        evt.ProposalId.Should().Be(proposalId);
        evt.EventType.Should().Be("ModificationExecuted");
    }
}

[Trait("Category", "Unit")]
public class GoalDecompositionRecordTests
{
    [Fact]
    public void ShortConstructor_SetsCreatedAtToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var goal = new Goal(Guid.NewGuid(), "Test", GoalType.Primary, 0.5, null,
            new List<Goal>(), new Dictionary<string, object>(), DateTime.UtcNow, false, null);

        var decomposition = new GoalDecomposition(goal, Array.Empty<GoalStep>(), new HypergridAnalysis());
        var after = DateTimeOffset.UtcNow;

        decomposition.CreatedAt.Should().BeOnOrAfter(before);
        decomposition.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void FullConstructor_SetsAllProperties()
    {
        var goal = new Goal(Guid.NewGuid(), "Test goal", GoalType.Primary, 0.8, null,
            new List<Goal>(), new Dictionary<string, object>(), DateTime.UtcNow, false, null);
        var steps = new List<GoalStep>
        {
            new("Step 1", GoalType.Primary, 0.5, DimensionalCoordinate.Origin)
        };
        var analysis = new HypergridAnalysis();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);

        var decomposition = new GoalDecomposition(goal, steps, analysis, createdAt);

        decomposition.OriginalGoal.Should().Be(goal);
        decomposition.Steps.Should().HaveCount(1);
        decomposition.DimensionalAnalysis.Should().Be(analysis);
        decomposition.CreatedAt.Should().Be(createdAt);
    }
}

[Trait("Category", "Unit")]
public class GoalStepToGoalTests
{
    [Fact]
    public void ToGoal_ConvertsCorrectly()
    {
        var coord = new DimensionalCoordinate(0.1, 0.2, 0.3, 0.4);
        var step = new GoalStep("Analyze data", GoalType.Analysis, 0.8, coord);

        var goal = step.ToGoal();

        goal.Description.Should().Be("Analyze data");
        goal.Type.Should().Be(GoalType.Analysis);
        goal.Priority.Should().Be(0.8);
        goal.Metadata.Should().ContainKey("temporal");
        goal.Metadata["temporal"].Should().Be(0.1);
        goal.Metadata["executionMode"].Should().Be("Automatic");
    }

    [Fact]
    public void ToGoal_WithParent_SetsParent()
    {
        var parent = new Goal(Guid.NewGuid(), "Parent", GoalType.Primary, 1.0, null,
            new List<Goal>(), new Dictionary<string, object>(), DateTime.UtcNow, false, null);
        var step = new GoalStep("Child", GoalType.Secondary, 0.5, DimensionalCoordinate.Origin);

        var goal = step.ToGoal(parent);

        goal.Parent.Should().Be(parent);
    }
}
