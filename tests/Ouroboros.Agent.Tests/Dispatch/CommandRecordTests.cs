// <copyright file="CommandRecordTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class CommandRecordTests
{
    [Fact]
    public void ProcessMindCommand_SetsProperties()
    {
        var cmd = new ProcessMindCommand("Hello", Complex: true);

        cmd.Prompt.Should().Be("Hello");
        cmd.Complex.Should().BeTrue();
    }

    [Fact]
    public void ProcessMindCommand_DefaultComplexIsFalse()
    {
        var cmd = new ProcessMindCommand("Hello");

        cmd.Complex.Should().BeFalse();
    }

    [Fact]
    public void SelectModelCommand_SetsProperties()
    {
        var context = new Dictionary<string, object> { ["key"] = "value" };
        var cmd = new SelectModelCommand("Pick model", context);

        cmd.Prompt.Should().Be("Pick model");
        cmd.Context.Should().BeSameAs(context);
    }

    [Fact]
    public void SelectModelCommand_DefaultContextIsNull()
    {
        var cmd = new SelectModelCommand("Pick model");

        cmd.Context.Should().BeNull();
    }

    [Fact]
    public void CreatePlanCommand_SetsProperties()
    {
        var ctx = new Dictionary<string, object> { ["env"] = "test" };
        var cmd = new CreatePlanCommand("Build a house", ctx);

        cmd.Goal.Should().Be("Build a house");
        cmd.Context.Should().BeSameAs(ctx);
    }

    [Fact]
    public void ExecutePlanCommand_SetsPlan()
    {
        var plan = new Ouroboros.Agent.MetaAI.Plan(
            "goal", new List<Ouroboros.Agent.MetaAI.PlanStep>(),
            new Dictionary<string, double>(), DateTime.UtcNow);
        var cmd = new ExecutePlanCommand(plan);

        cmd.Plan.Should().BeSameAs(plan);
    }
}
