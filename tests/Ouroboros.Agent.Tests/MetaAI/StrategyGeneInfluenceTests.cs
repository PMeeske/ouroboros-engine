// <copyright file="StrategyGeneInfluenceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.Evolution;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Tests that verify evolved strategy genes actively influence orchestrator behavior.
/// </summary>
[Trait("Category", "Unit")]
public class StrategyGeneInfluenceTests
{
    /// <summary>
    /// Constants matching the OuroborosOrchestrator implementation.
    /// </summary>
    private const int MinPlanSteps = 3;
    private const int PlanStepsRange = 7;
    private const double BaseQualityThreshold = 0.3;
    private const double QualityThresholdRange = 0.5;

    /// <summary>
    /// Mock chat completion model that records the prompts it receives.
    /// </summary>
    private class PromptCapturingChatModel : IChatCompletionModel
    {
        public List<string> CapturedPrompts { get; } = new();
        private readonly string _response;

        public PromptCapturingChatModel(string response = "Step 1: Test step\nStep 2: Another step")
        {
            _response = response;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            CapturedPrompts.Add(prompt);
            return Task.FromResult(_response);
        }
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
            => Task.FromResult(new float[384]);

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
            => Task.FromResult(new float[384]);
    }

    [Fact]
    public void OuroborosAtom_GetStrategyWeight_ReturnsDefaultWhenNotSet()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();

        // Act
        double weight = atom.GetStrategyWeight("PlanningDepth", 0.5);

        // Assert
        weight.Should().Be(0.5, "Should return default value when strategy not evolved");
    }

    [Fact]
    public void OuroborosAtom_GetStrategyWeight_ReturnsEvolvedValue()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        var capability = new OuroborosCapability("Strategy_PlanningDepth", "Planning depth strategy", 0.8);
        atom.AddCapability(capability);

        // Act
        double weight = atom.GetStrategyWeight("PlanningDepth", 0.5);

        // Assert
        weight.Should().Be(0.8, "Should return evolved strategy value from capability");
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithLowPlanningDepth_RequestsConcisePlan()
    {
        // Arrange
        var promptCapture = new PromptCapturingChatModel();
        var atom = OuroborosAtom.CreateDefault();
        // Set low planning depth (0.2 = shallow/fast)
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "Low depth", 0.2));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(promptCapture)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var planPrompt = promptCapture.CapturedPrompts.First();
        planPrompt.Should().Contain("concise high-level plan", "Low planning depth should request concise plan");
        // Low depth (0.2) still uses default granularity (0.5), so steps should be calculated normally
        // This test is about PlanningDepth affecting the type of plan, not the step count
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithHighPlanningDepth_RequestsDetailedPlan()
    {
        // Arrange
        var promptCapture = new PromptCapturingChatModel();
        var atom = OuroborosAtom.CreateDefault();
        // Set high planning depth (0.9 = deep/thorough)
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "High depth", 0.9));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(promptCapture)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var planPrompt = promptCapture.CapturedPrompts.First();
        planPrompt.Should().Contain("detailed plan", "High planning depth should request detailed plan");
        planPrompt.Should().Contain("sub-steps and contingencies", "High planning depth should suggest sub-steps");
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithLowDecompositionGranularity_RequestsFewerSteps()
    {
        // Arrange
        var promptCapture = new PromptCapturingChatModel();
        var atom = OuroborosAtom.CreateDefault();
        // Set low granularity (0.0 = coarse, fewer steps)
        atom.AddCapability(new OuroborosCapability("Strategy_DecompositionGranularity", "Coarse", 0.0));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(promptCapture)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var planPrompt = promptCapture.CapturedPrompts.First();
        planPrompt.Should().Contain("approximately 3 steps", "Low granularity should suggest ~3 steps");
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithHighDecompositionGranularity_RequestsMoreSteps()
    {
        // Arrange
        var promptCapture = new PromptCapturingChatModel();
        var atom = OuroborosAtom.CreateDefault();
        // Set high granularity (1.0 = fine, more steps)
        atom.AddCapability(new OuroborosCapability("Strategy_DecompositionGranularity", "Fine", 1.0));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(promptCapture)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var planPrompt = promptCapture.CapturedPrompts.First();
        planPrompt.Should().Contain("approximately 10 steps", "High granularity should suggest ~10 steps");
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithLowVerificationStrictness_AcceptsLowerQuality()
    {
        // Arrange
        // Return a verification response with quality score of 0.4
        var mockLlm = new PromptCapturingChatModel(@"{""verified"": true, ""quality_score"": 0.4, ""reasoning"": ""acceptable""}");
        var atom = OuroborosAtom.CreateDefault();
        // Set low strictness (0.0 = lenient, threshold will be 0.3)
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Lenient", 0.0));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var verifyPhase = result.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeTrue("Quality score 0.4 should pass with lenient threshold 0.3");
        verifyPhase.Metadata.Should().ContainKey("quality_threshold");
        ((double)verifyPhase.Metadata["quality_threshold"]).Should().BeApproximately(0.3, 0.01);
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithHighVerificationStrictness_RejectsLowerQuality()
    {
        // Arrange
        // Return a verification response with quality score of 0.5
        var mockLlm = new PromptCapturingChatModel(@"{""verified"": true, ""quality_score"": 0.5, ""reasoning"": ""acceptable""}");
        var atom = OuroborosAtom.CreateDefault();
        // Set high strictness (1.0 = strict, threshold will be 0.8)
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Strict", 1.0));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var verifyPhase = result.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeFalse("Quality score 0.5 should fail with strict threshold 0.8");
        verifyPhase.Metadata.Should().ContainKey("quality_threshold");
        ((double)verifyPhase.Metadata["quality_threshold"]).Should().BeApproximately(0.8, 0.01);
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithMediumStrictness_UsesCorrectThreshold()
    {
        // Arrange
        var mockLlm = new PromptCapturingChatModel(@"{""verified"": true, ""quality_score"": 0.55, ""reasoning"": ""acceptable""}");
        var atom = OuroborosAtom.CreateDefault();
        // Set medium strictness (0.5 = moderate, threshold will be 0.55)
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Moderate", 0.5));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var verifyPhase = result.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Metadata.Should().ContainKey("quality_threshold");
        // Calculate expected threshold using the same formula as production code
        double calculatedThreshold = BaseQualityThreshold + (0.5 * QualityThresholdRange);
        ((double)verifyPhase.Metadata["quality_threshold"]).Should().BeApproximately(
            calculatedThreshold, 
            0.01, 
            $"{BaseQualityThreshold} + (0.5 * {QualityThresholdRange}) = {calculatedThreshold}");
    }

    [Fact]
    public async Task OuroborosOrchestrator_VerifyPhase_IncludesStrictnessMetadata()
    {
        // Arrange
        var mockLlm = new PromptCapturingChatModel(@"{""verified"": true, ""quality_score"": 0.9, ""reasoning"": ""excellent""}");
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Test", 0.7));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var verifyPhase = result.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Metadata.Should().ContainKey("verification_strictness");
        verifyPhase.Metadata.Should().ContainKey("quality_threshold");
        verifyPhase.Metadata.Should().ContainKey("meets_quality_threshold");
        ((double)verifyPhase.Metadata["verification_strictness"]).Should().Be(0.7);
        ((bool)verifyPhase.Metadata["meets_quality_threshold"]).Should().BeTrue();
    }

    [Theory]
    [InlineData(0.0, 0.3)]  // BaseQualityThreshold + (0.0 * QualityThresholdRange) = 0.3
    [InlineData(0.2, 0.4)]  // BaseQualityThreshold + (0.2 * QualityThresholdRange) = 0.4
    [InlineData(0.5, 0.55)] // BaseQualityThreshold + (0.5 * QualityThresholdRange) = 0.55
    [InlineData(0.8, 0.7)]  // BaseQualityThreshold + (0.8 * QualityThresholdRange) = 0.7
    [InlineData(1.0, 0.8)]  // BaseQualityThreshold + (1.0 * QualityThresholdRange) = 0.8
    public async Task OuroborosOrchestrator_VerificationStrictness_CalculatesCorrectThreshold(double strictness, double expectedThreshold)
    {
        // Arrange
        var mockLlm = new PromptCapturingChatModel(@"{""verified"": true, ""quality_score"": 0.9, ""reasoning"": ""test""}");
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Test", strictness));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var verifyPhase = result.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        ((double)verifyPhase!.Metadata["quality_threshold"]).Should().BeApproximately(expectedThreshold, 0.01);
    }

    [Fact]
    public async Task OuroborosOrchestrator_WithoutEvolvedStrategies_UsesDefaultValues()
    {
        // Arrange
        var promptCapture = new PromptCapturingChatModel();
        var atom = OuroborosAtom.CreateDefault();
        // Don't add any strategy capabilities - should use defaults

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(promptCapture)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert - Should complete successfully with default values
        var planPrompt = promptCapture.CapturedPrompts.First();
        // Default PlanningDepth=0.5 (medium), should see "structured plan"
        planPrompt.Should().Contain("structured plan", "Default planning depth should use structured plan");
        // Default DecompositionGranularity=0.5: MinPlanSteps + (0.5 * PlanStepsRange) = 3 + 3.5 = 6.5 ≈ 6
        int expectedSteps = (int)(MinPlanSteps + (0.5 * PlanStepsRange));
        planPrompt.Should().Contain($"approximately {expectedSteps} steps", "Default granularity should suggest calculated steps");
    }

    [Fact]
    public async Task OuroborosOrchestrator_MultipleStrategyGenes_AllInfluenceBehavior()
    {
        // Arrange
        var promptCapture = new PromptCapturingChatModel(@"{""verified"": true, ""quality_score"": 0.6, ""reasoning"": ""ok""}");
        var atom = OuroborosAtom.CreateDefault();
        
        // Set all four strategy genes
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "Deep", 0.8));
        atom.AddCapability(new OuroborosCapability("Strategy_DecompositionGranularity", "Fine", 0.9));
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tools", 0.8));
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Moderate", 0.4));

        var orchestrator = new OuroborosOrchestratorBuilder()
            .WithLLM(promptCapture)
            .WithMemory(new MemoryStore(new MockEmbeddingModel()))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(new MockMeTTaEngine())
            .WithAtom(atom)
            .Build();

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal", OrchestratorContext.Create(ct: CancellationToken.None));

        // Assert
        var planPrompt = promptCapture.CapturedPrompts.First();
        planPrompt.Should().Contain("detailed plan", "PlanningDepth=0.8 should request detailed plan");
        // DecompositionGranularity=0.9: MinPlanSteps + (0.9 * PlanStepsRange) = 3 + 6.3 = 9.3 ≈ 9
        int expectedSteps = (int)(MinPlanSteps + (0.9 * PlanStepsRange));
        planPrompt.Should().Contain($"approximately {expectedSteps} steps", "DecompositionGranularity=0.9 should suggest calculated steps");

        var verifyPhase = result.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        // Calculate expected threshold: BaseQualityThreshold + (0.4 * QualityThresholdRange)
        double expectedThreshold = BaseQualityThreshold + (0.4 * QualityThresholdRange);
        ((double)verifyPhase!.Metadata["quality_threshold"]).Should().BeApproximately(
            expectedThreshold, 
            0.01, 
            $"VerificationStrictness=0.4 should set threshold to {BaseQualityThreshold} + (0.4 * {QualityThresholdRange}) = {expectedThreshold}");
    }
}
