// <copyright file="ThoughtFragmentTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NanoAtoms;
using Ouroboros.Providers;

namespace Ouroboros.Tests.NanoAtoms;

[Trait("Category", "Unit")]
public sealed class ThoughtFragmentTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var tags = new[] { "tag1", "tag2" };

        // Act
        var fragment = new ThoughtFragment(
            id, "test content", "user", 10,
            SubGoalType.Reasoning, SubGoalComplexity.Moderate, PathwayTier.CloudLight,
            timestamp, tags);

        // Assert
        fragment.Id.Should().Be(id);
        fragment.Content.Should().Be("test content");
        fragment.Source.Should().Be("user");
        fragment.EstimatedTokens.Should().Be(10);
        fragment.GoalType.Should().Be(SubGoalType.Reasoning);
        fragment.Complexity.Should().Be(SubGoalComplexity.Moderate);
        fragment.PreferredTier.Should().Be(PathwayTier.CloudLight);
        fragment.Timestamp.Should().Be(timestamp);
        fragment.Tags.Should().BeEquivalentTo(tags);
    }

    [Fact]
    public void FromText_CreatesFragmentFromString()
    {
        // Act
        var fragment = ThoughtFragment.FromText("Hello world");

        // Assert
        fragment.Content.Should().Be("Hello world");
        fragment.Source.Should().Be("user");
        fragment.Id.Should().NotBeEmpty();
        fragment.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void FromText_SetsEstimatedTokens()
    {
        // Act
        var fragment = ThoughtFragment.FromText("This is a test string for tokens");

        // Assert
        fragment.EstimatedTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FromText_WithIndex_SetsTag()
    {
        // Act
        var fragment = ThoughtFragment.FromText("test", 5);

        // Assert
        fragment.Tags.Should().Contain("chunk_5");
    }

    [Fact]
    public void FromText_ThrowsOnNullOrWhitespace()
    {
        // Act & Assert
        var act1 = () => ThoughtFragment.FromText(null!);
        act1.Should().Throw<ArgumentException>();

        var act2 = () => ThoughtFragment.FromText("   ");
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromSubGoal_CreatesFragmentFromSubGoal()
    {
        // Arrange
        var subGoal = SubGoal.FromDescription("Analyze the data", 0);

        // Act
        var fragment = ThoughtFragment.FromSubGoal(subGoal);

        // Assert
        fragment.Content.Should().Be("Analyze the data");
        fragment.Source.Should().Be("goal-decomposer");
        fragment.GoalType.Should().Be(subGoal.Type);
        fragment.Complexity.Should().Be(subGoal.Complexity);
        fragment.PreferredTier.Should().Be(subGoal.PreferredTier);
    }

    [Fact]
    public void FromSubGoal_ThrowsOnNull()
    {
        // Act & Assert
        var act = () => ThoughtFragment.FromSubGoal(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EstimateTokenCount_ReturnsApproximation()
    {
        // Act & Assert
        ThoughtFragment.EstimateTokenCount("test").Should().Be(1); // 4 chars / 4 = 1
        ThoughtFragment.EstimateTokenCount("hello world!!").Should().Be(3); // 13 chars / 4 = 3
    }

    [Fact]
    public void EstimateTokenCount_EmptyOrNull_ReturnsZero()
    {
        // Act & Assert
        ThoughtFragment.EstimateTokenCount("").Should().Be(0);
        ThoughtFragment.EstimateTokenCount(null!).Should().Be(0);
    }

    [Fact]
    public void EstimateTokenCount_MinimumIsOne()
    {
        // A single character still produces at least 1 token
        ThoughtFragment.EstimateTokenCount("a").Should().Be(1);
    }
}
