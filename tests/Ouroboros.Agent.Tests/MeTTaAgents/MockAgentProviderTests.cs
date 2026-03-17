// <copyright file="MockAgentProviderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MeTTaAgents;

namespace Ouroboros.Tests.MeTTaAgents;

[Trait("Category", "Unit")]
public class MockAgentProviderTests
{
    private readonly MockAgentProvider _sut = new();

    [Fact]
    public void CanHandle_WithLocalMock_ReturnsTrue()
    {
        // Act
        var result = _sut.CanHandle("LocalMock");

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("OllamaCloud")]
    [InlineData("OpenAI")]
    [InlineData("")]
    [InlineData("localmock")]
    public void CanHandle_WithOtherProviders_ReturnsFalse(string providerName)
    {
        // Act
        var result = _sut.CanHandle(providerName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateModelAsync_ReturnsSuccessWithModel()
    {
        // Arrange
        var agentDef = CreateAgentDef("agent-1", "Coder");

        // Act
        var result = await _sut.CreateModelAsync(agentDef);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateModelAsync_ReturnedModelGeneratesText()
    {
        // Arrange
        var agentDef = CreateAgentDef("agent-1", "Coder");
        var result = await _sut.CreateModelAsync(agentDef);

        // Act
        var model = result.Value;
        var response = await model.GenerateTextAsync("Write a function");

        // Assert
        response.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HealthCheckAsync_ReturnsHealthyStatus()
    {
        // Act
        var result = await _sut.HealthCheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProviderName.Should().Be("LocalMock");
        result.Value.IsHealthy.Should().BeTrue();
        result.Value.LatencyMs.Should().Be(0.0);
        result.Value.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task HealthCheckAsync_WithCancellationToken_Completes()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _sut.HealthCheckAsync(cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    private static MeTTaAgentDef CreateAgentDef(string agentId, string role)
        => new(agentId, "LocalMock", "mock-model", role,
            "You are a test agent.", 1024, 0.7f);
}

[Trait("Category", "Unit")]
public class MockChatModelTests
{
    [Theory]
    [InlineData("Coder", "mock-coder")]
    [InlineData("Reviewer", "mock-reviewer")]
    [InlineData("Planner", "mock-planner")]
    [InlineData("Reasoner", "mock-reasoner")]
    [InlineData("Summarizer", "mock-summarizer")]
    public async Task GenerateTextAsync_WithKnownRole_ReturnsRoleSpecificResponse(
        string role, string expectedPrefix)
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("test-agent", "LocalMock", "mock", role,
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;

        // Act
        var response = await model.GenerateTextAsync("test prompt");

        // Assert
        response.Should().Contain(expectedPrefix);
        response.Should().Contain("test-agent");
    }

    [Fact]
    public async Task GenerateTextAsync_WithUnknownRole_ReturnsGenericResponse()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("agent-x", "LocalMock", "mock", "CustomRole",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;

        // Act
        var response = await model.GenerateTextAsync("hello world");

        // Assert
        response.Should().Contain("mock-customrole");
        response.Should().Contain("agent-x");
    }

    [Fact]
    public async Task GenerateTextAsync_WithCoderRole_IncludesPromptContent()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("coder-1", "LocalMock", "mock", "Coder",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;
        var prompt = "Write a sorting algorithm";

        // Act
        var response = await model.GenerateTextAsync(prompt);

        // Assert
        response.Should().Contain("Implementation for:");
        response.Should().Contain("Write a sorting algorithm");
    }

    [Fact]
    public async Task GenerateTextAsync_WithPlannerRole_ReturnsStructuredPlan()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("planner-1", "LocalMock", "mock", "Planner",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;

        // Act
        var response = await model.GenerateTextAsync("Plan a feature");

        // Assert
        response.Should().Contain("Plan:");
        response.Should().Contain("1.");
        response.Should().Contain("2.");
        response.Should().Contain("3.");
    }

    [Fact]
    public async Task GenerateTextAsync_WithLongPrompt_TruncatesInOutput()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("agent-1", "LocalMock", "mock", "Coder",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;
        var longPrompt = new string('x', 200);

        // Act
        var response = await model.GenerateTextAsync(longPrompt);

        // Assert
        response.Should().Contain("...");
    }

    [Fact]
    public async Task GenerateTextAsync_WithShortPrompt_DoesNotTruncate()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("agent-1", "LocalMock", "mock", "Coder",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;
        var shortPrompt = "short";

        // Act
        var response = await model.GenerateTextAsync(shortPrompt);

        // Assert
        response.Should().NotEndWith("...");
        response.Should().Contain("short");
    }

    [Fact]
    public async Task GenerateTextAsync_WithReviewerRole_ContainsReviewLanguage()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("rev-1", "LocalMock", "mock", "Reviewer",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;

        // Act
        var response = await model.GenerateTextAsync("Review this code");

        // Assert
        response.Should().Contain("Review complete");
    }

    [Fact]
    public async Task GenerateTextAsync_WithReasonerRole_ContainsAnalysis()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("reason-1", "LocalMock", "mock", "Reasoner",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;

        // Act
        var response = await model.GenerateTextAsync("Analyze this");

        // Assert
        response.Should().Contain("Analysis:");
        response.Should().Contain("consistent");
    }

    [Fact]
    public async Task GenerateTextAsync_WithSummarizerRole_ContainsSummary()
    {
        // Arrange
        var provider = new MockAgentProvider();
        var agentDef = new MeTTaAgentDef("sum-1", "LocalMock", "mock", "Summarizer",
            "system prompt", 1024, 0.5f);
        var result = await provider.CreateModelAsync(agentDef);
        var model = result.Value;

        // Act
        var response = await model.GenerateTextAsync("Summarize this text");

        // Assert
        response.Should().Contain("Summary:");
    }
}
