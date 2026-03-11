using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class PromptResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        string prompt = "Explain quantum computing";
        bool success = true;
        double latencyMs = 250.5;
        double confidenceScore = 0.95;
        string? selectedModel = "gpt-4";
        string? error = null;

        // Act
        var sut = new PromptResult(prompt, success, latencyMs, confidenceScore, selectedModel, error);

        // Assert
        sut.Prompt.Should().Be(prompt);
        sut.Success.Should().BeTrue();
        sut.LatencyMs.Should().Be(latencyMs);
        sut.ConfidenceScore.Should().Be(confidenceScore);
        sut.SelectedModel.Should().Be(selectedModel);
        sut.Error.Should().BeNull();
    }

    [Fact]
    public void NullableFields_AcceptNullValues()
    {
        // Arrange & Act
        var sut = new PromptResult("test", true, 100.0, 0.9, null, null);

        // Assert
        sut.SelectedModel.Should().BeNull();
        sut.Error.Should().BeNull();
    }

    [Fact]
    public void FailureState_HasErrorMessage()
    {
        // Arrange & Act
        var sut = new PromptResult("bad prompt", false, 50.0, 0.0, null, "Timeout occurred");

        // Assert
        sut.Success.Should().BeFalse();
        sut.Error.Should().Be("Timeout occurred");
        sut.ConfidenceScore.Should().Be(0.0);
    }

    [Fact]
    public void SuccessState_HasModelAndConfidence()
    {
        // Arrange & Act
        var sut = new PromptResult("good prompt", true, 200.0, 0.85, "claude-3", null);

        // Assert
        sut.Success.Should().BeTrue();
        sut.SelectedModel.Should().Be("claude-3");
        sut.ConfidenceScore.Should().Be(0.85);
        sut.Error.Should().BeNull();
    }
}
