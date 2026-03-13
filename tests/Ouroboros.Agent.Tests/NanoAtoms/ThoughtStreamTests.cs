// <copyright file="ThoughtStreamTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;
using Ouroboros.Providers;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Tests for ThoughtStream — channel-based async stream.
/// </summary>
[Trait("Category", "Unit")]
public class ThoughtStreamTests
{
    private static ThoughtFragment CreateFragment(string content = "Test")
    {
        return new ThoughtFragment(
            Guid.NewGuid(), content, "test",
            ThoughtFragment.EstimateTokenCount(content),
            SubGoalType.Reasoning, SubGoalComplexity.Simple, PathwayTier.Local,
            DateTime.UtcNow, ["test"]);
    }

    [Fact]
    public async Task Stream_ProcessesFragments_ProducesDigests()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Processed output");

        var config = NanoAtomConfig.Default();
        var atom = new NanoOuroborosAtom(mockModel.Object, config);
        await using var stream = new ThoughtStream(atom);

        // Act
        stream.Start();
        await stream.WriteAsync(CreateFragment("First thought"));
        await stream.WriteAsync(CreateFragment("Second thought"));
        stream.Complete();

        var digests = await stream.CollectDigestsAsync();

        // Assert
        digests.Should().HaveCount(2);
        stream.DigestsProduced.Should().Be(2);
    }

    [Fact]
    public async Task Stream_WhenDisposed_StopsProcessing()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Output");

        var config = NanoAtomConfig.Default();
        var atom = new NanoOuroborosAtom(mockModel.Object, config);
        var stream = new ThoughtStream(atom);

        stream.Start();
        await stream.WriteAsync(CreateFragment());

        // Act
        await stream.DisposeAsync();

        // Assert: Should not throw
        stream.IsProcessing.Should().BeFalse();
    }
}
