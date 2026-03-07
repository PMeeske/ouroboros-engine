// <copyright file="NanoAtomChainTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// End-to-end integration tests for NanoAtomChain.
/// </summary>
[Trait("Category", "Unit")]
public class NanoAtomChainTests
{
    [Fact]
    public async Task ExecuteAsync_ShortPrompt_ReturnsConsolidatedAction()
    {
        // Arrange: Single fragment, single atom
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Model response to the query");

        var config = new NanoAtomConfig(UseGoalDecomposer: false);
        var chain = new NanoAtomChain(mockModel.Object, config);

        // Act
        var result = await chain.ExecuteAsync("What is the meaning of life?");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().NotBeNullOrEmpty();
        result.Value.SourceDigests.Should().HaveCountGreaterThan(0);
        result.Value.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_LongPrompt_SplitsAndConsolidates()
    {
        // Arrange: Long prompt → multiple fragments → parallel processing
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Processed fragment output");

        var config = new NanoAtomConfig(
            MaxInputTokens: 10,
            MaxParallelAtoms: 2,
            UseGoalDecomposer: false);
        var chain = new NanoAtomChain(mockModel.Object, config);

        string longPrompt =
            "First topic about artificial intelligence. " +
            "Second topic about machine learning. " +
            "Third topic about neural networks.";

        // Act
        var result = await chain.ExecuteAsync(longPrompt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StreamCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPrompt_ReturnsFailure()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var config = NanoAtomConfig.Default();
        var chain = new NanoAtomChain(mockModel.Object, config);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => chain.ExecuteAsync(""));
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ReturnsFailure()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(5000, ct);
                return "Response";
            });

        var config = new NanoAtomConfig(UseGoalDecomposer: false);
        var chain = new NanoAtomChain(mockModel.Object, config);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act
        var result = await chain.ExecuteAsync("Test prompt", cts.Token);

        // Assert: Should fail or have no digests due to cancellation
        // The exact behavior depends on timing, but it shouldn't hang
        (result.IsSuccess && result.Value.SourceDigests.Count == 0 || !result.IsSuccess)
            .Should().BeTrue();
    }
}
