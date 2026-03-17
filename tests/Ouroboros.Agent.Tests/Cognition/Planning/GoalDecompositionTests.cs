// <copyright file="GoalDecompositionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests.Cognition.Planning;

/// <summary>
/// Unit tests for <see cref="GoalDecomposition"/>, <see cref="GoalStep"/>,
/// and <see cref="ExecutionMode"/>.
/// </summary>
[Trait("Category", "Unit")]
public class GoalDecompositionTests
{
    // --- GoalDecomposition ---

    [Fact]
    public void GoalDecomposition_SetsProperties()
    {
        // Arrange
        var goal = new Goal("Build feature", GoalType.Primary, 0.9);
        var steps = new List<GoalStep>
        {
            new("Step 1", GoalType.Instrumental, 0.8, DimensionalCoordinate.Origin)
        };
        var analysis = new HypergridAnalysis();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var decomposition = new GoalDecomposition(goal, steps, analysis, timestamp);

        // Assert
        decomposition.OriginalGoal.Should().Be(goal);
        decomposition.Steps.Should().HaveCount(1);
        decomposition.DimensionalAnalysis.Should().Be(analysis);
        decomposition.CreatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void GoalDecomposition_ConvenienceConstructor_SetsCreatedAtToNow()
    {
        // Arrange
        var goal = new Goal("Build feature", GoalType.Primary, 0.9);
        var steps = new List<GoalStep>();
        var analysis = new HypergridAnalysis();

        // Act
        var before = DateTimeOffset.UtcNow;
        var decomposition = new GoalDecomposition(goal, steps, analysis);
        var after = DateTimeOffset.UtcNow;

        // Assert
        decomposition.CreatedAt.Should().BeOnOrAfter(before);
        decomposition.CreatedAt.Should().BeOnOrBefore(after);
    }

    // --- GoalStep ---

    [Fact]
    public void GoalStep_FullConstructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deps = new List<Guid> { Guid.NewGuid() };
        var coord = new DimensionalCoordinate(0.5, 0.3, 0.7, 0.1);

        // Act
        var step = new GoalStep(id, "Implement auth", GoalType.Primary, 0.9,
            coord, deps, ExecutionMode.RequiresApproval);

        // Assert
        step.Id.Should().Be(id);
        step.Description.Should().Be("Implement auth");
        step.Type.Should().Be(GoalType.Primary);
        step.Priority.Should().Be(0.9);
        step.Coordinate.Should().Be(coord);
        step.DependsOn.Should().HaveCount(1);
        step.Mode.Should().Be(ExecutionMode.RequiresApproval);
    }

    [Fact]
    public void GoalStep_ConvenienceConstructor_SetsDefaults()
    {
        // Arrange & Act
        var step = new GoalStep("Do something", GoalType.Safety, 1.0,
            DimensionalCoordinate.Origin);

        // Assert
        step.Id.Should().NotBe(Guid.Empty);
        step.Description.Should().Be("Do something");
        step.DependsOn.Should().BeEmpty();
        step.Mode.Should().Be(ExecutionMode.Automatic);
    }

    [Fact]
    public void GoalStep_ToGoal_CreatesGoalWithMetadata()
    {
        // Arrange
        var coord = new DimensionalCoordinate(0.5, 0.3, 0.7, 0.1);
        var step = new GoalStep("Implement feature", GoalType.Primary, 0.8, coord);

        // Act
        var goal = step.ToGoal();

        // Assert
        goal.Id.Should().Be(step.Id);
        goal.Description.Should().Be("Implement feature");
        goal.Type.Should().Be(GoalType.Primary);
        goal.Priority.Should().Be(0.8);
        goal.ParentGoal.Should().BeNull();
        goal.Constraints.Should().ContainKey("temporal");
        goal.Constraints.Should().ContainKey("semantic");
        goal.Constraints.Should().ContainKey("causal");
        goal.Constraints.Should().ContainKey("modal");
        goal.Constraints.Should().ContainKey("executionMode");
        goal.Constraints["executionMode"].Should().Be("Automatic");
    }

    [Fact]
    public void GoalStep_ToGoal_WithParent_SetsParentGoal()
    {
        // Arrange
        var parent = new Goal("Parent", GoalType.Primary, 1.0);
        var step = new GoalStep("Child step", GoalType.Instrumental, 0.5,
            DimensionalCoordinate.Origin);

        // Act
        var goal = step.ToGoal(parent);

        // Assert
        goal.ParentGoal.Should().Be(parent);
    }

    // --- ExecutionMode ---

    [Fact]
    public void ExecutionMode_HasExpectedValues()
    {
        Enum.GetValues<ExecutionMode>().Should().HaveCount(4);
        Enum.IsDefined(ExecutionMode.Automatic).Should().BeTrue();
        Enum.IsDefined(ExecutionMode.RequiresApproval).Should().BeTrue();
        Enum.IsDefined(ExecutionMode.ToolDelegation).Should().BeTrue();
        Enum.IsDefined(ExecutionMode.HumanDelegation).Should().BeTrue();
    }
}
