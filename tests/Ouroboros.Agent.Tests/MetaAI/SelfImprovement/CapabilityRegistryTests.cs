// <copyright file="CapabilityRegistryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using MetaAIPlan = Ouroboros.Agent.MetaAI.Plan;
using MetaAIPlanStep = Ouroboros.Agent.PlanStep;
using MetaAIAgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CapabilityRegistryTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly ToolRegistry _toolRegistry;
    private readonly CapabilityRegistry _registry;

    public CapabilityRegistryTests()
    {
        _toolRegistry = new ToolRegistry();
        _registry = new CapabilityRegistry(_llmMock.Object, _toolRegistry);
    }

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new CapabilityRegistry(null!, _toolRegistry);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTools_Throws()
    {
        var act = () => new CapabilityRegistry(_llmMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterCapability_AddsCapability()
    {
        var cap = CreateCapability("code", "Code generation", 0.9);

        _registry.RegisterCapability(cap);

        _registry.GetCapability("code").Should().NotBeNull();
        _registry.GetCapability("code")!.Name.Should().Be("code");
    }

    [Fact]
    public void RegisterCapability_Null_Throws()
    {
        var act = () => _registry.RegisterCapability(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetCapability_Unknown_ReturnsNull()
    {
        _registry.GetCapability("unknown").Should().BeNull();
    }

    [Fact]
    public async Task GetCapabilitiesAsync_ReturnsSorted()
    {
        _registry.RegisterCapability(CreateCapability("low", "Low", 0.3));
        _registry.RegisterCapability(CreateCapability("high", "High", 0.9));

        var caps = await _registry.GetCapabilitiesAsync();

        caps.Should().HaveCount(2);
        caps[0].Name.Should().Be("high");
    }

    [Fact]
    public async Task CanHandleAsync_EmptyTask_ReturnsFalse()
    {
        var result = await _registry.CanHandleAsync("  ");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCapabilityAsync_UpdatesMetrics()
    {
        var cap = CreateCapability("code", "Code gen", 0.8, 10);
        _registry.RegisterCapability(cap);

        var plan = new MetaAIPlan("g", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var execResult = new PlanExecutionResult(plan, new List<StepResult>(), true, "done",
            new Dictionary<string, object>(), TimeSpan.FromSeconds(2));

        await _registry.UpdateCapabilityAsync("code", execResult);

        var updated = _registry.GetCapability("code");
        updated!.UsageCount.Should().Be(11);
    }

    [Fact]
    public async Task UpdateCapabilityAsync_UnknownCapability_DoesNothing()
    {
        var plan = new MetaAIPlan("g", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var execResult = new PlanExecutionResult(plan, new List<StepResult>(), true, "done",
            new Dictionary<string, object>(), TimeSpan.FromSeconds(1));

        // Should not throw
        await _registry.UpdateCapabilityAsync("unknown", execResult);
    }

    private static MetaAIAgentCapability CreateCapability(
        string name, string description, double successRate, int usageCount = 5)
    {
        return new MetaAIAgentCapability(
            name,
            description,
            new List<string>(),
            successRate,
            50.0,
            new List<string>(),
            usageCount,
            DateTime.UtcNow,
            DateTime.UtcNow,
            new Dictionary<string, object>());
    }
}
