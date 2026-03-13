// <copyright file="AdditionalCommandTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public sealed class AdditionalCommandTests
{
    [Fact]
    public void SelectModelCommand_RecordEquality()
    {
        // Arrange
        var a = new SelectModelCommand("prompt");
        var b = new SelectModelCommand("prompt");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void SelectModelCommand_WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var cmd = new SelectModelCommand("original");

        // Act
        var modified = cmd with { Prompt = "updated" };

        // Assert
        modified.Prompt.Should().Be("updated");
    }

    [Fact]
    public void CreatePlanCommand_RecordEquality()
    {
        // Arrange
        var a = new CreatePlanCommand("goal");
        var b = new CreatePlanCommand("goal");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void CreatePlanCommand_WithContext_SetsContext()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["env"] = "test" };

        // Act
        var cmd = new CreatePlanCommand("goal", context);

        // Assert
        cmd.Goal.Should().Be("goal");
        cmd.Context.Should().BeSameAs(context);
    }

    [Fact]
    public void ExecutePlanCommand_SetsPlan()
    {
        // Arrange
        var plan = new Ouroboros.Agent.MetaAI.Plan(
            "goal", new List<Ouroboros.Agent.PlanStep>(),
            new Dictionary<string, double>(), DateTime.UtcNow);

        // Act
        var cmd = new ExecutePlanCommand(plan);

        // Assert
        cmd.Plan.Should().BeSameAs(plan);
    }
}
