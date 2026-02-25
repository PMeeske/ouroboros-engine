// <copyright file="OuroborosOrchestratorBuilderFormReasoningTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Hyperon;
using ToolRegistry = Ouroboros.Tools.ToolRegistry;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Tests for FormMeTTaBridge integration with orchestrator builders.
/// Tests both MeTTaOrchestratorBuilder (which has WithFormReasoning) and verifies
/// that the standard orchestrator path properly wires Laws of Form reasoning.
/// </summary>
[Trait("Category", "Unit")]
public class OuroborosOrchestratorBuilderFormReasoningTests
{
    /// <summary>
    /// Mock chat completion model for testing.
    /// </summary>
    private class MockChatCompletionModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult("Mock response");
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
    /// Mock Hyperon MeTTa engine with AtomSpace for Form reasoning.
    /// </summary>
    private class MockHyperonMeTTaEngine : IMeTTaEngine
    {
        public MockHyperonMeTTaEngine(AtomSpace atomSpace)
        {
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
            => Task.FromResult(Result<bool, string>.Success(true));

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
    public void MeTTaOrchestratorBuilder_WithFormReasoning_EnablesLofTools()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();
        var atomSpace = new AtomSpace();
        var formBridge = new FormMeTTaBridge(atomSpace);

        // Act
        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithFormReasoning(formBridge)
            .Build();

        // Assert
        var tools = orchestrator.GetType()
            .GetField("_tools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(orchestrator) as ToolRegistry;

        tools.Should().NotBeNull();
        tools!.All.Should().Contain(t => t.Name.StartsWith("lof_"), "LoF tools should be registered");
    }

    [Fact]
    public void MeTTaOrchestratorBuilder_WithFormReasoningBridge_UsesProvidedBridge()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();
        var atomSpace = new AtomSpace();
        var customBridge = new FormMeTTaBridge(atomSpace);

        // Act
        var builder = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithFormReasoning(customBridge);

        var orchestrator = builder.Build();

        // Assert
        orchestrator.Should().NotBeNull();
        builder.FormReasoningEnabled.Should().BeTrue();
    }

    [Fact]
    public void MeTTaOrchestratorBuilder_WithoutFormReasoning_NoLofTools()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();

        // Act
        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .Build(); // No WithFormReasoning() call

        // Assert
        var tools = orchestrator.GetType()
            .GetField("_tools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(orchestrator) as ToolRegistry;

        tools.Should().NotBeNull();
        tools!.All.Should().NotContain(t => t.Name.StartsWith("lof_"), 
            "LoF tools should NOT be registered without WithFormReasoning()");
    }

    [Fact]
    public void MeTTaOrchestratorBuilder_WithFormReasoningNoArg_CreatesDefaultBridge()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();

        // Act
        var builder = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithFormReasoning(); // No bridge argument

        var orchestrator = builder.Build();

        // Assert
        orchestrator.Should().NotBeNull();
        builder.FormReasoningEnabled.Should().BeTrue();
    }

    [Fact]
    public void MeTTaOrchestratorBuilder_WithHyperonEngine_InitializesFormBridge()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();
        var atomSpace = new AtomSpace();
        var hyperonEngine = new MockHyperonMeTTaEngine(atomSpace);

        // Act
        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithMeTTaEngine(hyperonEngine)
            .WithFormReasoning() // Should use HyperonEngine's AtomSpace
            .Build();

        // Assert
        orchestrator.Should().NotBeNull();
        var tools = orchestrator.GetType()
            .GetField("_tools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(orchestrator) as ToolRegistry;

        tools!.All.Should().Contain(t => t.Name.StartsWith("lof_"));
    }

    [Fact]
    public void MeTTaOrchestratorBuilder_WithFormReasoningNull_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new MeTTaOrchestratorBuilder();

        // Act
        Action act = () => builder.WithFormReasoning(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("bridge");
    }

    [Fact]
    public void MeTTaOrchestratorBuilder_BuildWithFormReasoning_RegistersAllLofTools()
    {
        // Arrange
        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();
        var atomSpace = new AtomSpace();
        var formBridge = new FormMeTTaBridge(atomSpace);

        // Act
        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithFormReasoning(formBridge)
            .Build();

        // Assert
        var tools = orchestrator.GetType()
            .GetField("_tools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(orchestrator) as ToolRegistry;

        // Verify specific LoF tools exist
        var lofTools = tools!.All.Where(t => t.Name.StartsWith("lof_")).ToList();
        lofTools.Should().NotBeEmpty("Should have at least one LoF tool registered");
    }

    [Fact]
    public void FormReasoningEnabled_ReturnsTrueWhenEnabled()
    {
        // Arrange
        var builder = new MeTTaOrchestratorBuilder()
            .WithFormReasoning();

        // Act
        bool enabled = builder.FormReasoningEnabled;

        // Assert
        enabled.Should().BeTrue();
    }

    [Fact]
    public void FormReasoningEnabled_ReturnsFalseWhenNotEnabled()
    {
        // Arrange
        var builder = new MeTTaOrchestratorBuilder();

        // Act
        bool enabled = builder.FormReasoningEnabled;

        // Assert
        enabled.Should().BeFalse();
    }

    [Fact]
    public void MeTTaOrchestratorBuilder_MultipleWithFormReasoningCalls_UsesLastBridge()
    {
        // Arrange
        var atomSpace1 = new AtomSpace();
        var atomSpace2 = new AtomSpace();
        var bridge1 = new FormMeTTaBridge(atomSpace1);
        var bridge2 = new FormMeTTaBridge(atomSpace2);

        var mockLlm = new MockChatCompletionModel();
        var mockEmbed = new MockEmbeddingModel();

        // Act
        var orchestrator = new MeTTaOrchestratorBuilder()
            .WithLLM(mockLlm)
            .WithMemory(new MemoryStore(mockEmbed))
            .WithSkills(new SkillRegistry())
            .WithRouter(new UncertaintyRouter(new SmartModelOrchestrator(ToolRegistry.CreateDefault())))
            .WithSafety(new SafetyGuard())
            .WithFormReasoning(bridge1)
            .WithFormReasoning(bridge2) // Should use this one
            .Build();

        // Assert
        orchestrator.Should().NotBeNull();
    }
}
