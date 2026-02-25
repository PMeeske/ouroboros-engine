// <copyright file="OuroborosOrchestratorVerificationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Hyperon;
using ToolRegistry = Ouroboros.Tools.ToolRegistry;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Tests for OuroborosOrchestrator verification phase fail-closed behavior.
/// Verifies that malformed JSON and MeTTa errors are treated as failures.
/// </summary>
[Trait("Category", "Unit")]
public class OuroborosOrchestratorVerificationTests
{
    /// <summary>
    /// Mock chat completion model for testing verification parsing.
    /// </summary>
    private class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly Func<string, CancellationToken, Task<string>> _generateFunc;

        public MockChatCompletionModel(Func<string, CancellationToken, Task<string>>? generateFunc = null)
        {
            _generateFunc = generateFunc ?? ((prompt, ct) => Task.FromResult("{\"verified\": true, \"quality_score\": 0.8}"));
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => _generateFunc(prompt, ct);
    }

    /// <summary>
    /// Mock MeTTa engine for testing verification.
    /// </summary>
    private class MockMeTTaEngine : IMeTTaEngine
    {
        private readonly Func<string, CancellationToken, Task<Result<bool, string>>> _verifyFunc;

        public MockMeTTaEngine(Func<string, CancellationToken, Task<Result<bool, string>>>? verifyFunc = null)
        {
            _verifyFunc = verifyFunc ?? ((plan, ct) => Task.FromResult(Result<bool, string>.Success(true)));
        }

        public Task<string> ExecuteAsync(string mettaCode, CancellationToken ct = default)
            => Task.FromResult("(verified)");

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("(verified)"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => _verifyFunc(plan, ct);

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock embedding model for testing.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[384]);

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
            => Task.FromResult(new float[384]);
    }

    [Fact]
    public async Task ParseVerificationResult_MalformedJson_ReturnsFalse()
    {
        // Arrange - LLM returns malformed JSON on first call, then good JSON on retry
        int callCount = 0;
        var mockLlm = new MockChatCompletionModel((prompt, ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First call returns malformed JSON
                return Task.FromResult("This is not valid JSON at all");
            }
            else
            {
                // Second call (retry) also returns malformed JSON
                return Task.FromResult("Still not valid JSON");
            }
        });

        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore(mockEmbed);
        var safety = new SafetyGuard();

        var orchestrator = new OuroborosOrchestrator(
            mockLlm,
            tools,
            memory,
            safety,
            mockMeTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("Malformed JSON should fail verification");
        
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeFalse();
        verifyPhase.Metadata.Should().ContainKey("quality_score");
        verifyPhase.Metadata["quality_score"].Should().Be(0.0);
        
        callCount.Should().Be(2, "Should retry once before failing");
    }

    [Fact]
    public async Task ParseVerificationResult_MalformedJsonThenValidJson_RetriesAndSucceeds()
    {
        // Arrange - LLM returns malformed JSON on first call, then good JSON on retry
        int callCount = 0;
        var mockLlm = new MockChatCompletionModel((prompt, ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First call returns malformed JSON
                return Task.FromResult("This is not valid JSON at all");
            }
            else
            {
                // Second call (retry) returns valid JSON
                return Task.FromResult("{\"verified\": true, \"quality_score\": 0.9}");
            }
        });

        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore(mockEmbed);
        var safety = new SafetyGuard();

        var orchestrator = new OuroborosOrchestrator(
            mockLlm,
            tools,
            memory,
            safety,
            mockMeTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Retry with valid JSON should succeed");
        
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeTrue();
        verifyPhase.Metadata.Should().ContainKey("quality_score");
        verifyPhase.Metadata["quality_score"].Should().Be(0.9);
        
        callCount.Should().Be(2, "Should retry once after initial failure");
    }

    [Fact]
    public async Task MeTTaVerificationError_ReturnsFalse()
    {
        // Arrange - MeTTa verification returns error
        var mockLlm = new MockChatCompletionModel((prompt, ct) =>
            Task.FromResult("{\"verified\": true, \"quality_score\": 0.8}"));

        var mockMeTTa = new MockMeTTaEngine((plan, ct) =>
            Task.FromResult(Result<bool, string>.Failure("MeTTa engine error: connection failed")));

        var mockEmbed = new MockEmbeddingModel();
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore(mockEmbed);
        var safety = new SafetyGuard();

        var orchestrator = new OuroborosOrchestrator(
            mockLlm,
            tools,
            memory,
            safety,
            mockMeTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("MeTTa error should fail verification");
        
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeFalse("Verify phase should fail when MeTTa fails");
        verifyPhase.Metadata.Should().ContainKey("metta_verified");
        verifyPhase.Metadata["metta_verified"].Should().Be(false);
        verifyPhase.Metadata["llm_verified"].Should().Be(true, "LLM verification should still succeed");
    }

    [Fact]
    public async Task VerifyPhase_BothLlmAndMeTTaPass_ReturnsSuccess()
    {
        // Arrange - Both verifications succeed
        var mockLlm = new MockChatCompletionModel((prompt, ct) =>
            Task.FromResult("{\"verified\": true, \"quality_score\": 0.9}"));

        var mockMeTTa = new MockMeTTaEngine((plan, ct) =>
            Task.FromResult(Result<bool, string>.Success(true)));

        var mockEmbed = new MockEmbeddingModel();
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore(mockEmbed);
        var safety = new SafetyGuard();

        var orchestrator = new OuroborosOrchestrator(
            mockLlm,
            tools,
            memory,
            safety,
            mockMeTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("Both verifications pass");
        
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeTrue();
        verifyPhase.Metadata["metta_verified"].Should().Be(true);
        verifyPhase.Metadata["llm_verified"].Should().Be(true);
        verifyPhase.Metadata["quality_score"].Should().Be(0.9);
    }

    [Fact]
    public async Task VerifyPhase_LlmFailsMeTTaPasses_ReturnsFailure()
    {
        // Arrange - LLM fails, MeTTa passes
        var mockLlm = new MockChatCompletionModel((prompt, ct) =>
            Task.FromResult("{\"verified\": false, \"quality_score\": 0.3}"));

        var mockMeTTa = new MockMeTTaEngine((plan, ct) =>
            Task.FromResult(Result<bool, string>.Success(true)));

        var mockEmbed = new MockEmbeddingModel();
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore(mockEmbed);
        var safety = new SafetyGuard();

        var orchestrator = new OuroborosOrchestrator(
            mockLlm,
            tools,
            memory,
            safety,
            mockMeTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("LLM verification failed");
        
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeFalse("Both gates must pass");
        verifyPhase.Metadata["metta_verified"].Should().Be(true);
        verifyPhase.Metadata["llm_verified"].Should().Be(false);
    }

    [Fact]
    public async Task LearnPhase_VerificationFailure_RecordsFailureExperience()
    {
        // Arrange - Verification fails
        var mockLlm = new MockChatCompletionModel((prompt, ct) =>
        {
            // Return malformed JSON for verification, valid for other phases
            if (prompt.Contains("Verify"))
            {
                return Task.FromResult("invalid json");
            }
            return Task.FromResult("mock response");
        });

        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore(mockEmbed);
        var safety = new SafetyGuard();

        var orchestrator = new OuroborosOrchestrator(
            mockLlm,
            tools,
            memory,
            safety,
            mockMeTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("Verification failed");
        
        // Check that the experience was recorded as a failure
        var atom = orchestrator.Atom;
        atom.Experiences.Should().HaveCountGreaterThanOrEqualTo(1);
        var experience = atom.Experiences.Last();
        experience.Success.Should().BeFalse("Experience should reflect verification failure");
        experience.QualityScore.Should().BeLessThanOrEqualTo(0.5, "Failed verification should have low quality score");
    }

    [Fact]
    public async Task VerifyPhase_MissingQualityScore_FailsClosed()
    {
        // Arrange - JSON has verified but missing quality_score
        var mockLlm = new MockChatCompletionModel((prompt, ct) =>
            Task.FromResult("{\"verified\": true}"));

        var mockMeTTa = new MockMeTTaEngine();
        var mockEmbed = new MockEmbeddingModel();
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore(mockEmbed);
        var safety = new SafetyGuard();

        var orchestrator = new OuroborosOrchestrator(
            mockLlm,
            tools,
            memory,
            safety,
            mockMeTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test goal");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("Missing quality_score should fail verification");
        
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeFalse();
        verifyPhase.Metadata["quality_score"].Should().Be(0.0);
    }
}
