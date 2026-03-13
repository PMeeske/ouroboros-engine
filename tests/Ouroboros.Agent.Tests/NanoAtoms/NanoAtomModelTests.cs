// <copyright file="NanoAtomModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Tests for NanoAtomModel — IChatCompletionModel decorator over NanoAtom pipeline.
/// </summary>
[Trait("Category", "Unit")]
public class NanoAtomModelTests
{
    [Fact]
    public async Task GenerateTextAsync_ValidPrompt_ReturnsConsolidatedContent()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Processed response from nano atom");

        var config = new NanoAtomConfig(UseGoalDecomposer: false);
        var nanoModel = new NanoAtomModel(mockModel.Object, config);

        // Act
        string result = await nanoModel.GenerateTextAsync("What is machine learning?");

        // Assert
        result.Should().NotBeNullOrEmpty();
        nanoModel.TotalRequests.Should().Be(1);
    }

    [Fact]
    public async Task GenerateTextAsync_EmptyPrompt_ReturnsEmptyString()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var nanoModel = new NanoAtomModel(mockModel.Object);

        // Act
        string result = await nanoModel.GenerateTextAsync("   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateTextAsync_PipelineFails_FallsBackToDirectModel()
    {
        // Arrange: Inner model throws on first call (pipeline call), succeeds on second (fallback)
        int callCount = 0;
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((prompt, _) =>
            {
                callCount++;
                if (callCount <= 2)
                    throw new InvalidOperationException("Model overloaded");
                return Task.FromResult("Fallback direct response");
            });

        var config = new NanoAtomConfig(UseGoalDecomposer: false, EnableCircuitBreaker: false);
        var nanoModel = new NanoAtomModel(mockModel.Object, config);

        // Act: Pipeline will fail, should fall back to direct model call
        string result = await nanoModel.GenerateTextAsync("Test prompt");

        // Assert: Should get fallback response
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessFullAsync_ValidPrompt_ReturnsConsolidatedAction()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Full pipeline response");

        var config = new NanoAtomConfig(UseGoalDecomposer: false);
        var nanoModel = new NanoAtomModel(mockModel.Object, config);

        // Act
        var result = await nanoModel.ProcessFullAsync("Explain AI");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().NotBeNullOrEmpty();
        result.Value.SourceDigests.Should().HaveCountGreaterThan(0);
        result.Value.Confidence.Should().BeGreaterThan(0);
        nanoModel.LastAction.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessFullAsync_EmptyPrompt_ReturnsFailure()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var nanoModel = new NanoAtomModel(mockModel.Object);

        // Act
        var result = await nanoModel.ProcessFullAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void CreateMinimal_ReturnsModelWithMinimalConfig()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();

        // Act
        var nanoModel = NanoAtomModel.CreateMinimal(mockModel.Object);

        // Assert
        nanoModel.Should().NotBeNull();
        nanoModel.Should().BeAssignableTo<Ouroboros.Abstractions.Core.IChatCompletionModel>();
    }

    [Fact]
    public void ImplementsIChatCompletionModel()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();

        // Act
        var nanoModel = new NanoAtomModel(mockModel.Object);

        // Assert: Can be assigned to IChatCompletionModel
        Ouroboros.Abstractions.Core.IChatCompletionModel model = nanoModel;
        model.Should().NotBeNull();
    }

    [Fact]
    public async Task Metrics_TrackRequestsAndLatency()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        var config = new NanoAtomConfig(UseGoalDecomposer: false);
        var nanoModel = new NanoAtomModel(mockModel.Object, config);

        // Act
        await nanoModel.GenerateTextAsync("First prompt");
        await nanoModel.GenerateTextAsync("Second prompt");

        // Assert
        nanoModel.TotalRequests.Should().Be(2);
        nanoModel.AverageLatencyMs.Should().BeGreaterThanOrEqualTo(0);
        nanoModel.SuccessRate.Should().BeGreaterThan(0);
    }
}
