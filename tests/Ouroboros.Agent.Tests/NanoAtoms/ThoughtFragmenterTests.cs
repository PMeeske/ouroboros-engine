// <copyright file="ThoughtFragmenterTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Tests for ThoughtFragmenter — GoalDecomposer integration and naive chunking.
/// </summary>
[Trait("Category", "Unit")]
public class ThoughtFragmenterTests
{
    [Fact]
    public async Task FragmentAsync_ShortPrompt_ReturnsSingleFragment()
    {
        // Arrange: Short prompt fits in a single atom
        var config = NanoAtomConfig.Default();
        var fragmenter = new ThoughtFragmenter(config);

        // Act
        var fragments = await fragmenter.FragmentAsync("What is 2+2?");

        // Assert
        fragments.Should().HaveCount(1);
        fragments[0].Content.Should().Be("What is 2+2?");
    }

    [Fact]
    public async Task FragmentAsync_LongPrompt_SplitsIntoMultipleFragments()
    {
        // Arrange: Create a prompt that exceeds the token budget
        var config = new NanoAtomConfig(MaxInputTokens: 10, UseGoalDecomposer: false);
        var fragmenter = new ThoughtFragmenter(config);

        // ~10 tokens = ~40 chars. Create content longer than that.
        string longPrompt = "This is the first sentence about topic A. " +
                            "This is the second sentence about topic B. " +
                            "This is the third sentence about topic C. " +
                            "This is the fourth sentence about topic D.";

        // Act
        var fragments = await fragmenter.FragmentAsync(longPrompt);

        // Assert
        fragments.Should().HaveCountGreaterThan(1);
        foreach (var fragment in fragments)
        {
            fragment.Content.Should().NotBeNullOrEmpty();
            fragment.EstimatedTokens.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task FragmentAsync_WithGoalDecomposer_UsesLLMDecomposition()
    {
        // Arrange: Mock a decomposition model that returns sub-goals
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"Sub-goal 1: analyze input\", \"Sub-goal 2: generate output\"]");

        var config = new NanoAtomConfig(MaxInputTokens: 5, UseGoalDecomposer: true);
        var fragmenter = new ThoughtFragmenter(config, mockModel.Object);

        string longPrompt = "Analyze the input data and generate a comprehensive output report with findings.";

        // Act
        var fragments = await fragmenter.FragmentAsync(longPrompt);

        // Assert
        fragments.Should().HaveCount(2);
        fragments[0].Source.Should().Be("goal-decomposer");
        fragments[1].Source.Should().Be("goal-decomposer");
    }

    [Fact]
    public async Task FragmentAsync_GoalDecomposerFails_FallsBackToNaiveChunking()
    {
        // Arrange: Model throws → should fall back to naive chunking
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Model unavailable"));

        var config = new NanoAtomConfig(MaxInputTokens: 10, UseGoalDecomposer: true);
        var fragmenter = new ThoughtFragmenter(config, mockModel.Object);

        string longPrompt = "First sentence about topic A. Second sentence about topic B. Third sentence about topic C.";

        // Act
        var fragments = await fragmenter.FragmentAsync(longPrompt);

        // Assert: Should still produce fragments via naive chunking
        fragments.Should().HaveCountGreaterThan(0);
        fragments[0].Source.Should().Be("user"); // Naive chunking, not goal-decomposer
    }
}
