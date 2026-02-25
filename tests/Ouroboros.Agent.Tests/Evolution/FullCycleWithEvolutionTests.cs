// <copyright file="FullCycleWithEvolutionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Hyperon;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;
using ToolRegistry = Ouroboros.Tools.ToolRegistry;

namespace Ouroboros.Tests.Evolution;

/// <summary>
/// End-to-end integration tests for orchestrator with both GA evolution and Form reasoning enabled.
/// Tests the complete Plan→Execute→Verify→Learn cycle with all new features active.
/// </summary>
[Trait("Category", "Integration")]
public class FullCycleWithEvolutionTests
{
    /// <summary>
    /// Mock chat completion model for testing.
    /// </summary>
    private class MockChatCompletionModel : IChatCompletionModel
    {
        private int _callCount = 0;

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            _callCount++;
            
            // Return different responses based on what phase is calling
            if (prompt.Contains("Plan") || prompt.Contains("plan"))
            {
                return Task.FromResult("Step 1: Analyze the goal\nStep 2: Execute the solution");
            }
            else if (prompt.Contains("insight") || prompt.Contains("learn"))
            {
                return Task.FromResult("- Learned to decompose goals effectively\n- Tool usage improved efficiency");
            }
            else
            {
                return Task.FromResult($"Mock response {_callCount}");
            }
        }
    }

    /// <summary>
    /// Mock MeTTa engine with verification support.
    /// </summary>
    private class MockMeTTaEngine : IMeTTaEngine
    {
        public Task<string> ExecuteAsync(string mettaCode, CancellationToken ct = default)
        {
            // Simulate successful verification
            if (mettaCode.Contains("verify"))
            {
                return Task.FromResult("(verified True)");
            }
            return Task.FromResult("(success)");
        }

        public Task LoadFileAsync(string path, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("(success)"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock Hyperon MeTTa engine for Form reasoning.
    /// </summary>
    private class MockHyperonMeTTaEngine : IMeTTaEngine
    {
        public MockHyperonMeTTaEngine(AtomSpace atomSpace)
        {
        }

        public Task<string> ExecuteAsync(string mettaCode, CancellationToken ct = default)
        {
            // Handle Form reasoning queries
            if (mettaCode.Contains("distinction") || mettaCode.Contains("lof"))
            {
                return Task.FromResult("(distinction-verified)");
            }
            return Task.FromResult("(verified True)");
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("(success)"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock embedding model.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[384]);

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
            => Task.FromResult(new float[384]);
    }

    [Fact]
    public async Task Orchestrator_WithBothEvolutionAndFormReasoning_RunsFullCycle()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();
        var atomSpace = new AtomSpace();
        var mockHyperonEngine = new MockHyperonMeTTaEngine(atomSpace);
        var formBridge = new FormMeTTaBridge(atomSpace);

        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockHyperonEngine)
            .WithFormReasoning(formBridge)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync(
            new Plan("Test goal with both evolution and form reasoning", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Orchestrator_WithEvolution_AccumulatesExperiences()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act - Run multiple cycles
        await orchestrator.ExecuteAsync("Goal 1", OrchestratorContext.Create(ct: CancellationToken.None));
        await orchestrator.ExecuteAsync("Goal 2", OrchestratorContext.Create(ct: CancellationToken.None));
        await orchestrator.ExecuteAsync("Goal 3", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        orchestrator.Atom.Experiences.Should().HaveCount(3);
        orchestrator.Atom.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Orchestrator_AfterMultipleCycles_MaintainsFunctionality()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act - Run 5 cycles to trigger GA evolution
        for (int i = 0; i < 5; i++)
        {
            var result = await orchestrator.ExecuteAsync($"Goal {i}", OrchestratorContext.Create(ct: CancellationToken.None));
            result.Should().NotBeNull("Orchestrator should remain functional after cycle {0}", i);
        }

        // Assert - Orchestrator should still be operational
        var finalResult = await orchestrator.ExecuteAsync("Final goal", OrchestratorContext.Create(ct: CancellationToken.None));
        finalResult.Should().NotBeNull();
        orchestrator.Atom.Experiences.Should().HaveCount(6);
    }

    [Fact]
    public async Task MeTTaOrchestrator_WithFormReasoning_IncludesDistinctionChecks()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();
        var atomSpace = new AtomSpace();
        var mockHyperonEngine = new MockHyperonMeTTaEngine(atomSpace);
        var formBridge = new FormMeTTaBridge(atomSpace);

        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockHyperonEngine)
            .WithFormReasoning(formBridge)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync(
            new Plan("Test distinction-based verification", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Verify that LoF tools are available
        var tools = orchestrator.GetType()
            .GetField("_tools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(orchestrator) as ToolRegistry;

        tools!.All.Should().Contain(t => t.Name.StartsWith("lof_"));
    }

    [Fact]
    public async Task OuroborosOrchestrator_HealthCheck_IncludesExpectedFields()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();

        var atom = OuroborosAtom.CreateDefault();
        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithAtom(atom)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act - Run a cycle to populate data
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert - Check atom state includes expected data
        atom.InstanceId.Should().NotBeEmpty();
        atom.Experiences.Should().HaveCount(1);
        atom.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Orchestrator_MultipleGoals_HandlesEachIndependently()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act
        var result1 = await orchestrator.ExecuteAsync("Analyze data", OrchestratorContext.Create(ct: CancellationToken.None));
        var result2 = await orchestrator.ExecuteAsync("Generate report", OrchestratorContext.Create(ct: CancellationToken.None));
        var result3 = await orchestrator.ExecuteAsync("Send notification", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result3.Should().NotBeNull();
        
        orchestrator.Atom.Experiences.Should().HaveCount(3);
        orchestrator.Atom.Experiences.Select(e => e.Goal).Should().Contain("Analyze data");
        orchestrator.Atom.Experiences.Select(e => e.Goal).Should().Contain("Generate report");
        orchestrator.Atom.Experiences.Select(e => e.Goal).Should().Contain("Send notification");
    }

    [Fact]
    public async Task Orchestrator_WithEvolutionEnabled_TracksPerformanceMetrics()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var experience = orchestrator.Atom.Experiences.First();
        experience.QualityScore.Should().BeInRange(0.0, 1.0);
        experience.Success.Should().BeTrue();
        experience.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task MeTTaOrchestrator_WithFormReasoning_HandlesComplexGoals()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();
        var atomSpace = new AtomSpace();
        var mockHyperonEngine = new MockHyperonMeTTaEngine(atomSpace);
        var formBridge = new FormMeTTaBridge(atomSpace);

        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockHyperonEngine)
            .WithFormReasoning(formBridge)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync(
            new Plan("Complex goal requiring distinction-based reasoning and verification", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Orchestrator_CancellationToken_RespectedDuringExecution()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: cts.Token));
        });
    }
}
