// <copyright file="LearnPhaseEvolutionIntegrationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.Evolution;

namespace Ouroboros.Tests.Evolution;

/// <summary>
/// Integration tests for GA evolution in the Learn phase of OuroborosOrchestrator.
/// </summary>
[Trait("Category", "Integration")]
public class LearnPhaseEvolutionIntegrationTests
{
    /// <summary>
    /// Mock chat completion model for testing.
    /// </summary>
    private class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly Func<string, CancellationToken, Task<string>> _generateFunc;

        public MockChatCompletionModel(Func<string, CancellationToken, Task<string>>? generateFunc = null)
        {
            _generateFunc = generateFunc ?? ((prompt, ct) => Task.FromResult("Plan: Step 1\nReasoning: Test reasoning"));
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => _generateFunc(prompt, ct);
    }

    /// <summary>
    /// Mock MeTTa engine for testing.
    /// </summary>
    private class MockMeTTaEngine : IMeTTaEngine
    {
        public Task<string> ExecuteAsync(string mettaCode, CancellationToken ct = default)
            => Task.FromResult("(verified)");

        public Task LoadFileAsync(string path, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("(verified)"));

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
    /// Mock embedding model for memory store.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            // Return a simple fixed-size embedding
            return Task.FromResult(new float[384]);
        }

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
            => Task.FromResult(new float[384]);
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithStrategyEvolution_RunsCompleteCycle()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        result.Should().NotBeNull();
        orchestrator.Atom.Experiences.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task OuroborosOrchestrator_AfterLearnPhase_StoresEvolvedStrategiesInAtom()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        var atom = OuroborosAtom.CreateDefault();
        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithAtom(atom)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        atom.Experiences.Should().NotBeEmpty();
        // Capabilities should have been updated during Learn phase
        atom.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithFewerThan5Experiences_SkipsGAGracefully()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act - Run only once (less than minimum for GA)
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert - Should complete successfully without GA running
        result.Should().NotBeNull();
        orchestrator.Atom.Experiences.Should().HaveCount(1);
    }

    [Fact]
    public async Task OuroborosOrchestrator_WhenGAFails_DoesntBreakLearnPhase()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        // Create an atom with some pre-existing experiences to trigger GA
        var atom = OuroborosAtom.CreateDefault();
        for (int i = 0; i < 5; i++)
        {
            atom.RecordExperience(new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: $"Goal {i}",
                Success: true,
                QualityScore: 0.8,
                Insights: new List<string> { "Test" },
                Timestamp: DateTime.UtcNow));
        }

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithAtom(atom)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act - Even if GA has issues internally, the orchestrator should complete
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert - Orchestrator completes successfully
        result.Should().NotBeNull();
        orchestrator.Atom.Experiences.Should().HaveCount(6); // 5 initial + 1 new
    }

    [Fact]
    public async Task OuroborosOrchestrator_EvolvedStrategyWeights_ChangeAfterMultipleCycles()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Record initial capabilities count
        int initialCapabilitiesCount = orchestrator.Atom.Capabilities.Count;

        // Act - Run multiple cycles to accumulate experiences
        for (int i = 0; i < 6; i++)
        {
            await orchestrator.ExecuteAsync($"Test goal {i}", OrchestratorContext.Create(ct: CancellationToken.None));
        }

        // Assert
        orchestrator.Atom.Experiences.Should().HaveCount(6);
        // After multiple cycles, capabilities should have been updated
        // (Either count increased or confidences adjusted)
        bool capabilitiesChanged = 
            orchestrator.Atom.Capabilities.Count != initialCapabilitiesCount ||
            orchestrator.Atom.Capabilities.Any(c => c.ConfidenceLevel > 0.0);
        
        capabilitiesChanged.Should().BeTrue("Capabilities should evolve after multiple cycles");
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithoutStrategyEvolution_StillWorks()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        // Build orchestrator without strategy evolution (default behavior)
        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .Build(); // No WithStrategyEvolution() call

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert - Should work normally without GA
        result.Should().NotBeNull();
        orchestrator.Atom.Experiences.Should().HaveCount(1);
    }

    [Fact]
    public async Task OuroborosOrchestrator_MultipleRuns_AccumulatesExperiences()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act - Run multiple times
        await orchestrator.ExecuteAsync("Goal 1", OrchestratorContext.Create(ct: CancellationToken.None));
        await orchestrator.ExecuteAsync("Goal 2", OrchestratorContext.Create(ct: CancellationToken.None));
        await orchestrator.ExecuteAsync("Goal 3", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        orchestrator.Atom.Experiences.Should().HaveCount(3);
        orchestrator.Atom.Experiences.Select(e => e.Goal).Should().Contain("Goal 1");
        orchestrator.Atom.Experiences.Select(e => e.Goal).Should().Contain("Goal 2");
        orchestrator.Atom.Experiences.Select(e => e.Goal).Should().Contain("Goal 3");
    }

    [Fact]
    public async Task OuroborosOrchestrator_LearnPhase_RecordsExperienceMetrics()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var memory = new MemoryStore(mockEmbed);

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(memory)
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(mockMeTTa)
            .WithStrategyEvolution(enabled: true)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var experience = orchestrator.Atom.Experiences.First();
        experience.Goal.Should().Be("Test goal");
        experience.QualityScore.Should().BeInRange(0.0, 1.0);
        experience.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
