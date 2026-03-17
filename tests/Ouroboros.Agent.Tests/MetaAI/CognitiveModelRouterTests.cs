// <copyright file="CognitiveModelRouterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using static Ouroboros.Agent.MetaAI.CognitiveModelRouter;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for <see cref="CognitiveModelRouter"/>.
/// </summary>
[Trait("Category", "Unit")]
public class CognitiveModelRouterTests
{
    private readonly CognitiveModelRouter _sut = new();

    // --- Constructor / default routes ---

    [Fact]
    public void Constructor_RegistersDefaultRoutes()
    {
        // Act
        var allRoutes = _sut.GetAllRoutes();

        // Assert
        allRoutes.Should().NotBeEmpty();
        allRoutes.Should().ContainKey(CognitiveTask.EmotionDetection);
        allRoutes.Should().ContainKey(CognitiveTask.SentimentAnalysis);
        allRoutes.Should().ContainKey(CognitiveTask.MoralReasoning);
    }

    [Theory]
    [InlineData(CognitiveTask.EmotionDetection, "huggingface")]
    [InlineData(CognitiveTask.SentimentAnalysis, "huggingface")]
    [InlineData(CognitiveTask.CounterfactualSim, "ollama-cloud")]
    [InlineData(CognitiveTask.PredictiveProcessing, "ollama-cloud")]
    public void GetRoute_DefaultRoutes_ReturnsExpectedProvider(CognitiveTask task, string expectedProvider)
    {
        // Act
        var route = _sut.GetRoute(task);

        // Assert
        route.Provider.Should().Be(expectedProvider);
        route.Task.Should().Be(task);
    }

    // --- RegisterRoute ---

    [Fact]
    public void RegisterRoute_NullModelId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.RegisterRoute(CognitiveTask.GeneralReasoning, null!, "llm");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterRoute_NullProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.RegisterRoute(CognitiveTask.GeneralReasoning, "model", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterRoute_CustomRoute_OverridesPrimary()
    {
        // Arrange
        var router = new CognitiveModelRouter();

        // Act
        router.RegisterRoute(CognitiveTask.GeneralReasoning, "custom-model", "llm", "http://custom:8080");

        // Assert
        var route = router.GetRoute(CognitiveTask.GeneralReasoning);
        route.ModelId.Should().Be("custom-model");
        route.Endpoint.Should().Be("http://custom:8080");
    }

    [Fact]
    public void RegisterRoute_WithDefaultEndpoint_UsesProviderDefault()
    {
        // Arrange
        var router = new CognitiveModelRouter();

        // Act
        router.RegisterRoute(CognitiveTask.GeneralReasoning, "test-model", "huggingface");

        // Assert
        var route = router.GetRoute(CognitiveTask.GeneralReasoning);
        route.Endpoint.Should().Be("https://api-inference.huggingface.co/models/");
    }

    [Fact]
    public void RegisterRoute_OllamaLocal_UsesLocalEndpoint()
    {
        // Arrange
        var router = new CognitiveModelRouter();

        // Act
        router.RegisterRoute(CognitiveTask.GeneralReasoning, "llama3", "ollama-local");

        // Assert
        var route = router.GetRoute(CognitiveTask.GeneralReasoning);
        route.Endpoint.Should().Be("http://localhost:11434/");
        route.ExpectedLatencyMs.Should().Be(300);
    }

    // --- GetRoute ---

    [Fact]
    public void GetRoute_UnregisteredTask_ReturnsFallbackRoute()
    {
        // Arrange — GeneralReasoning has no default routes registered
        var router = new CognitiveModelRouter();

        // Act
        var route = router.GetRoute(CognitiveTask.GeneralReasoning);

        // Assert
        route.ModelId.Should().Be("general");
        route.Provider.Should().Be("llm");
        route.ExpectedLatencyMs.Should().Be(2000);
    }

    // --- GetFallbackChain ---

    [Fact]
    public void GetFallbackChain_RegisteredTask_ContainsAllRoutesAndFallback()
    {
        // Arrange
        var router = new CognitiveModelRouter();
        router.RegisterRoute(CognitiveTask.GeneralReasoning, "modelA", "huggingface");
        router.RegisterRoute(CognitiveTask.GeneralReasoning, "modelB", "ollama-cloud");

        // Act
        var chain = router.GetFallbackChain(CognitiveTask.GeneralReasoning);

        // Assert
        chain.Should().HaveCountGreaterThanOrEqualTo(3);
        chain[0].ModelId.Should().Be("modelA");
        chain[1].ModelId.Should().Be("modelB");
        chain.Last().ModelId.Should().Be("general-llm");
        chain.Last().Provider.Should().Be("llm");
    }

    [Fact]
    public void GetFallbackChain_UnregisteredTask_ContainsOnlyFallback()
    {
        // Arrange
        var router = new CognitiveModelRouter();

        // Act
        var chain = router.GetFallbackChain(CognitiveTask.GeneralReasoning);

        // Assert
        chain.Should().HaveCount(1);
        chain[0].Provider.Should().Be("llm");
    }

    // --- GetAllRoutes ---

    [Fact]
    public void GetAllRoutes_ReturnsPrimaryRoutePerTask()
    {
        // Act
        var allRoutes = _sut.GetAllRoutes();

        // Assert
        foreach (var kvp in allRoutes)
        {
            kvp.Value.Task.Should().Be(kvp.Key);
        }
    }

    // --- Latency estimation ---

    [Theory]
    [InlineData("huggingface", 500)]
    [InlineData("ollama-cloud", 1000)]
    [InlineData("ollama-local", 300)]
    public void RegisterRoute_ProviderType_SetsExpectedLatency(string provider, double expectedLatency)
    {
        // Arrange
        var router = new CognitiveModelRouter();

        // Act
        router.RegisterRoute(CognitiveTask.GeneralReasoning, "model", provider);

        // Assert
        var route = router.GetRoute(CognitiveTask.GeneralReasoning);
        route.ExpectedLatencyMs.Should().Be(expectedLatency);
    }

    [Fact]
    public void RegisterRoute_UnknownProvider_UsesDefaultLatencyAndEmptyEndpoint()
    {
        // Arrange
        var router = new CognitiveModelRouter();

        // Act
        router.RegisterRoute(CognitiveTask.GeneralReasoning, "model", "unknown-provider");

        // Assert
        var route = router.GetRoute(CognitiveTask.GeneralReasoning);
        route.ExpectedLatencyMs.Should().Be(3000);
        route.Endpoint.Should().BeEmpty();
    }

    // --- ModelRoute record ---

    [Fact]
    public void ModelRoute_RecordEquality_Works()
    {
        // Arrange
        var a = new ModelRoute(CognitiveTask.EmotionDetection, "model", "hf", "http://x", 500);
        var b = new ModelRoute(CognitiveTask.EmotionDetection, "model", "hf", "http://x", 500);

        // Assert
        a.Should().Be(b);
    }
}
