// <copyright file="OllamaAgentProviderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MeTTaAgents;

namespace Ouroboros.Tests.MeTTaAgents;

[Trait("Category", "Unit")]
public class OllamaAgentProviderTests
{
    [Fact]
    public void CanHandle_WithOllama_ReturnsTrue()
    {
        // Arrange
        var sut = new OllamaAgentProvider();

        // Act
        var result = sut.CanHandle("Ollama");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithOllamaCloud_ReturnsTrue()
    {
        // Arrange
        var sut = new OllamaAgentProvider();

        // Act
        var result = sut.CanHandle("OllamaCloud");

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("LocalMock")]
    [InlineData("")]
    [InlineData("ollama")]
    [InlineData("OLLAMA")]
    public void CanHandle_WithOtherProviders_ReturnsFalse(string providerName)
    {
        // Arrange
        var sut = new OllamaAgentProvider();

        // Act
        var result = sut.CanHandle(providerName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateModelAsync_WithLocalOllama_ReturnsSuccess()
    {
        // Arrange
        var sut = new OllamaAgentProvider("http://localhost:11434");
        var agentDef = CreateAgentDef("agent-1", "Ollama", "llama3");

        // Act
        var result = await sut.CreateModelAsync(agentDef);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateModelAsync_WithCustomEndpoint_ReturnsSuccess()
    {
        // Arrange
        var sut = new OllamaAgentProvider();
        var agentDef = new MeTTaAgentDef(
            "agent-2", "Ollama", "codellama", "Coder",
            "You code.", 2048, 0.3f,
            Endpoint: "http://custom-host:11434");

        // Act
        var result = await sut.CreateModelAsync(agentDef);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateModelAsync_WithOllamaCloud_NoApiKey_ReturnsFailure()
    {
        // Arrange
        var sut = new OllamaAgentProvider();
        // Use a non-existent env var name to ensure it's not set
        var agentDef = new MeTTaAgentDef(
            "cloud-agent", "OllamaCloud", "llama3", "Coder",
            "You code.", 2048, 0.5f,
            ApiKeyEnvVar: "OUROBOROS_TEST_NONEXISTENT_KEY_12345");

        // Act
        var result = await sut.CreateModelAsync(agentDef);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("API key env var");
        result.Error.Should().Contain("OUROBOROS_TEST_NONEXISTENT_KEY_12345");
        result.Error.Should().Contain("cloud-agent");
    }

    [Fact]
    public async Task CreateModelAsync_WithOllamaCloud_DefaultApiKeyEnvVar_ReturnsFailure()
    {
        // Arrange — the default env var OLLAMA_CLOUD_API_KEY is unlikely to be set in test
        var sut = new OllamaAgentProvider();
        var agentDef = new MeTTaAgentDef(
            "cloud-agent-2", "OllamaCloud", "llama3", "Reviewer",
            "You review.", 2048, 0.5f,
            ApiKeyEnvVar: null);

        // Act
        var result = await sut.CreateModelAsync(agentDef);

        // Assert
        // If OLLAMA_CLOUD_API_KEY is not set (typical in CI), this should fail
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_CLOUD_API_KEY")))
        {
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("OLLAMA_CLOUD_API_KEY");
        }
        else
        {
            // If it happens to be set, it should succeed
            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task CreateModelAsync_WithSameEndpoint_CachesClient()
    {
        // Arrange
        var sut = new OllamaAgentProvider("http://localhost:11434");
        var agentDef1 = CreateAgentDef("agent-a", "Ollama", "llama3");
        var agentDef2 = CreateAgentDef("agent-b", "Ollama", "codellama");

        // Act — create two models against same endpoint
        var result1 = await sut.CreateModelAsync(agentDef1);
        var result2 = await sut.CreateModelAsync(agentDef2);

        // Assert — both succeed (client is cached internally)
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateModelAsync_WithNullEndpoint_UsesDefault()
    {
        // Arrange
        var sut = new OllamaAgentProvider("http://localhost:11434");
        var agentDef = new MeTTaAgentDef(
            "agent-null-ep", "Ollama", "llama3", "Coder",
            "You code.", 2048, 0.5f,
            Endpoint: null);

        // Act
        var result = await sut.CreateModelAsync(agentDef);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateModelAsync_WithCancellationToken_Completes()
    {
        // Arrange
        var sut = new OllamaAgentProvider("http://localhost:11434");
        var agentDef = CreateAgentDef("agent-ct", "Ollama", "llama3");
        using var cts = new CancellationTokenSource();

        // Act
        var result = await sut.CreateModelAsync(agentDef, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheckAsync_WithUnreachableEndpoint_ReturnsUnhealthyStatus()
    {
        // Arrange — use a port that is almost certainly not running Ollama
        var sut = new OllamaAgentProvider("http://localhost:19999");

        // Act
        var result = await sut.HealthCheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue(); // The method wraps errors in ProviderHealthStatus
        result.Value.ProviderName.Should().Be("Ollama");
        result.Value.IsHealthy.Should().BeFalse();
        result.Value.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HealthCheckAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var sut = new OllamaAgentProvider("http://localhost:19999");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.HealthCheckAsync(cts.Token));
    }

    private static MeTTaAgentDef CreateAgentDef(string agentId, string provider, string model)
        => new(agentId, provider, model, "Coder",
            "You are a test agent.", 4096, 0.5f);
}
