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
}
