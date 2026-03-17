// <copyright file="HypergridRouterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Agent.MetaAI.SelfImprovement;

namespace Ouroboros.Agent.Tests.Cognition.Planning;

/// <summary>
/// Unit tests for <see cref="HypergridRouter"/>.
/// </summary>
[Trait("Category", "Unit")]
public class HypergridRouterTests
{
    private readonly HypergridRouter _sut = new();

    // --- Route: basic behavior ---

    [Fact]
    public void Route_EmptySteps_ReturnsEmptyResult()
    {
        // Act
        var (steps, analysis) = _sut.Route(
            Array.Empty<RawGoalStep>(),
            HypergridContext.Default);

        // Assert
        steps.Should().BeEmpty();
        analysis.OverallComplexity.Should().Be(0);
        analysis.CausalDepth.Should().Be(0);
    }

    [Fact]
    public void Route_SingleStep_ReturnsAnnotatedStep()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Implement feature", GoalType.Primary, 0.8)
        };

        // Act
        var (steps, analysis) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        steps.Should().HaveCount(1);
        steps[0].Description.Should().Be("Implement feature");
        steps[0].Type.Should().Be(GoalType.Primary);
        steps[0].Priority.Should().Be(0.8);
        steps[0].Coordinate.Should().NotBeNull();
    }

    [Fact]
    public void Route_MultipleSteps_AssignsUniqueTemporalCoordinates()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Step A", GoalType.Instrumental, 0.5),
            new("Step B", GoalType.Instrumental, 0.5),
            new("Step C", GoalType.Instrumental, 0.5)
        };

        // Act
        var (steps, _) = _sut.Route(raw, HypergridContext.Default);

        // Assert — temporal coordinates should be based on sequential position
        steps[0].Coordinate.Temporal.Should().BeLessThan(steps[2].Coordinate.Temporal);
    }

    // --- Semantic axis ---

    [Fact]
    public void Route_WithMatchingSkills_HigherSemanticCoordinate()
    {
        // Arrange
        var context = new HypergridContext(
            Deadline: null,
            AvailableSkills: new[] { "coding", "testing" },
            AvailableTools: Array.Empty<string>());

        var raw = new List<RawGoalStep>
        {
            new("Implement coding feature with testing", GoalType.Primary, 0.8),
            new("Write documentation about history", GoalType.Secondary, 0.5)
        };

        // Act
        var (steps, _) = _sut.Route(raw, context);

        // Assert — first step matches both skills, second matches none
        steps[0].Coordinate.Semantic.Should().BeGreaterThan(steps[1].Coordinate.Semantic);
    }

    [Fact]
    public void Route_NoSkills_NeutralSemanticCoordinate()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Some task", GoalType.Primary, 0.5)
        };

        // Act
        var (steps, _) = _sut.Route(raw, HypergridContext.Default);

        // Assert — no skills = neutral 0.5
        steps[0].Coordinate.Semantic.Should().Be(0.5);
    }

    // --- Causal axis (dependencies) ---

    [Fact]
    public void Route_WithDependencies_AssignsCausalDepth()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        var raw = new List<RawGoalStep>
        {
            new(id1, "Step 1 (root)", GoalType.Instrumental, 0.5, Array.Empty<Guid>()),
            new(id2, "Step 2 (depends on 1)", GoalType.Instrumental, 0.5, new[] { id1 }),
            new(id3, "Step 3 (depends on 2)", GoalType.Instrumental, 0.5, new[] { id2 })
        };

        // Act
        var (steps, analysis) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        analysis.CausalDepth.Should().Be(2);
        steps[0].Coordinate.Causal.Should().Be(0.0); // root
        steps[2].Coordinate.Causal.Should().Be(1.0); // deepest
    }

    [Fact]
    public void Route_NoDependencies_AllCausalZero()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Step A", GoalType.Instrumental, 0.5),
            new("Step B", GoalType.Instrumental, 0.5)
        };

        // Act
        var (steps, analysis) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        analysis.CausalDepth.Should().Be(0);
        steps[0].Coordinate.Causal.Should().Be(0.0);
        steps[1].Coordinate.Causal.Should().Be(0.0);
    }

    // --- Modal axis (execution mode) ---

    [Fact]
    public void Route_SafetyGoalType_RequiresApproval()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Safety critical operation", GoalType.Safety, 0.9)
        };

        // Act
        var (steps, _) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        steps[0].Mode.Should().Be(ExecutionMode.RequiresApproval);
    }

    [Fact]
    public void Route_WithMatchingTool_UsesToolDelegation()
    {
        // Arrange
        var context = new HypergridContext(
            Deadline: null,
            AvailableSkills: Array.Empty<string>(),
            AvailableTools: new[] { "compiler" });

        var raw = new List<RawGoalStep>
        {
            new("Use compiler to build", GoalType.Instrumental, 0.3)
        };

        // Act
        var (steps, _) = _sut.Route(raw, context);

        // Assert
        steps[0].Mode.Should().Be(ExecutionMode.ToolDelegation);
    }

    [Fact]
    public void Route_LowRiskNoTool_UsesAutomatic()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Simple task", GoalType.Instrumental, 0.3)
        };

        // Act
        var (steps, _) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        steps[0].Mode.Should().Be(ExecutionMode.Automatic);
    }

    [Fact]
    public void Route_HighPriorityAboveThreshold_RequiresApproval()
    {
        // Arrange — priority > threshold and no tool match
        var context = new HypergridContext(
            Deadline: null,
            AvailableSkills: Array.Empty<string>(),
            AvailableTools: Array.Empty<string>(),
            RiskThreshold: 0.3); // low threshold

        var raw = new List<RawGoalStep>
        {
            new("High priority task", GoalType.Primary, 0.9)
        };

        // Act
        var (steps, _) = _sut.Route(raw, context);

        // Assert
        steps[0].Mode.Should().Be(ExecutionMode.RequiresApproval);
    }

    // --- HypergridAnalysis ---

    [Fact]
    public void Route_Analysis_HasNonNegativeComplexity()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Step A", GoalType.Instrumental, 0.5),
            new("Step B", GoalType.Primary, 0.8),
            new("Step C", GoalType.Secondary, 0.3)
        };

        // Act
        var (_, analysis) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        analysis.OverallComplexity.Should().BeGreaterThanOrEqualTo(0.0);
        analysis.OverallComplexity.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Route_Analysis_ModalRequirements_ListsNonAutomaticSteps()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Safety check", GoalType.Safety, 0.9),
            new("Simple task", GoalType.Instrumental, 0.3)
        };

        // Act
        var (_, analysis) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        analysis.ModalRequirements.Should().HaveCountGreaterThanOrEqualTo(1);
        analysis.ModalRequirements.Any(m => m.Contains("Safety check")).Should().BeTrue();
    }

    [Fact]
    public void Route_Analysis_TemporalSpan_IsNonNegative()
    {
        // Arrange
        var raw = new List<RawGoalStep>
        {
            new("Step A", GoalType.Instrumental, 0.5),
            new("Step B", GoalType.Instrumental, 0.5)
        };

        // Act
        var (_, analysis) = _sut.Route(raw, HypergridContext.Default);

        // Assert
        analysis.TemporalSpan.Should().BeGreaterThanOrEqualTo(0.0);
    }

    // --- Temporal axis with deadline ---

    [Fact]
    public void Route_WithNearDeadline_HigherUrgency()
    {
        // Arrange — deadline in 1 hour = high urgency
        var nearDeadline = new HypergridContext(
            Deadline: DateTimeOffset.UtcNow.AddHours(1),
            AvailableSkills: Array.Empty<string>(),
            AvailableTools: Array.Empty<string>());

        var farDeadline = new HypergridContext(
            Deadline: DateTimeOffset.UtcNow.AddDays(14),
            AvailableSkills: Array.Empty<string>(),
            AvailableTools: Array.Empty<string>());

        var raw = new List<RawGoalStep>
        {
            new("Task", GoalType.Primary, 0.5)
        };

        // Act
        var (nearSteps, _) = _sut.Route(raw, nearDeadline);
        var (farSteps, _) = _sut.Route(raw, farDeadline);

        // Assert — near deadline should have higher temporal coordinate
        nearSteps[0].Coordinate.Temporal.Should().BeGreaterThan(farSteps[0].Coordinate.Temporal);
    }

    // --- DimensionalCoordinate ---

    [Fact]
    public void DimensionalCoordinate_DistanceTo_ComputesEuclidean()
    {
        // Arrange
        var a = new DimensionalCoordinate(0, 0, 0, 0);
        var b = new DimensionalCoordinate(1, 0, 0, 0);

        // Act & Assert
        a.DistanceTo(b).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void DimensionalCoordinate_DistanceTo_SamePoint_IsZero()
    {
        var a = new DimensionalCoordinate(0.5, 0.5, 0.5, 0.5);
        a.DistanceTo(a).Should().Be(0.0);
    }

    [Fact]
    public void DimensionalCoordinate_Origin_IsAllZeros()
    {
        var origin = DimensionalCoordinate.Origin;
        origin.Temporal.Should().Be(0);
        origin.Semantic.Should().Be(0);
        origin.Causal.Should().Be(0);
        origin.Modal.Should().Be(0);
    }

    // --- HypergridContext ---

    [Fact]
    public void HypergridContext_Default_HasNullDeadlineAndEmptyCollections()
    {
        var ctx = HypergridContext.Default;
        ctx.Deadline.Should().BeNull();
        ctx.AvailableSkills.Should().BeEmpty();
        ctx.AvailableTools.Should().BeEmpty();
        ctx.RiskThreshold.Should().Be(0.7);
    }

    // --- ExecutionMode enum ---

    [Theory]
    [InlineData(ExecutionMode.Automatic)]
    [InlineData(ExecutionMode.RequiresApproval)]
    [InlineData(ExecutionMode.ToolDelegation)]
    [InlineData(ExecutionMode.HumanDelegation)]
    public void ExecutionMode_AllValuesAreDefined(ExecutionMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }

    // --- GoalStep.ToGoal ---

    [Fact]
    public void GoalStep_ToGoal_ConvertsCorrectly()
    {
        // Arrange
        var coordinate = new DimensionalCoordinate(0.5, 0.3, 0.7, 0.1);
        var step = new GoalStep(
            Guid.NewGuid(), "Test step", GoalType.Primary, 0.8,
            coordinate, Array.Empty<Guid>(), ExecutionMode.Automatic);

        // Act
        var goal = step.ToGoal();

        // Assert
        goal.Id.Should().Be(step.Id);
        goal.Description.Should().Be("Test step");
        goal.Type.Should().Be(GoalType.Primary);
        goal.Priority.Should().Be(0.8);
        goal.Metadata.Should().ContainKey("temporal");
        goal.Metadata["temporal"].Should().Be(0.5);
    }

    // --- Cycle guard in dependency resolution ---

    [Fact]
    public void Route_CyclicDependencies_DoesNotHang()
    {
        // Arrange — A depends on B, B depends on A (cycle)
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var raw = new List<RawGoalStep>
        {
            new(idA, "Step A", GoalType.Instrumental, 0.5, new[] { idB }),
            new(idB, "Step B", GoalType.Instrumental, 0.5, new[] { idA })
        };

        // Act — should not hang or throw
        var act = () => _sut.Route(raw, HypergridContext.Default);

        // Assert
        act.Should().NotThrow();
    }
}
