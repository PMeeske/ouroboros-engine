// <copyright file="ExplorationOpportunityTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ExplorationOpportunityTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var description = "Explore novel attention mechanisms";
        var noveltyScore = 0.85;
        var infoGain = 0.72;
        var prerequisites = new List<string> { "ML basics", "Linear algebra" };
        var identifiedAt = DateTime.UtcNow;

        // Act
        var opportunity = new ExplorationOpportunity(description, noveltyScore, infoGain, prerequisites, identifiedAt);

        // Assert
        opportunity.Description.Should().Be(description);
        opportunity.NoveltyScore.Should().Be(noveltyScore);
        opportunity.InformationGainEstimate.Should().Be(infoGain);
        opportunity.Prerequisites.Should().BeEquivalentTo(prerequisites);
        opportunity.IdentifiedAt.Should().Be(identifiedAt);
    }

    [Fact]
    public void Constructor_WithEmptyPrerequisites_Succeeds()
    {
        var opportunity = new ExplorationOpportunity("desc", 0.5, 0.5, new List<string>(), DateTime.UtcNow);
        opportunity.Prerequisites.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = DateTime.UtcNow;
        var prereqs = new List<string> { "a" };
        var a = new ExplorationOpportunity("desc", 0.5, 0.5, prereqs, time);
        var b = new ExplorationOpportunity("desc", 0.5, 0.5, prereqs, time);

        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new ExplorationOpportunity("desc", 0.5, 0.5, new List<string>(), DateTime.UtcNow);

        var modified = original with { NoveltyScore = 0.9 };

        modified.NoveltyScore.Should().Be(0.9);
        modified.Description.Should().Be(original.Description);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    public void Constructor_AcceptsVariousScores(double novelty, double infoGain)
    {
        var opportunity = new ExplorationOpportunity("test", novelty, infoGain, new List<string>(), DateTime.UtcNow);
        opportunity.NoveltyScore.Should().Be(novelty);
        opportunity.InformationGainEstimate.Should().Be(infoGain);
    }
}
