// <copyright file="InsightTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class InsightTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var category = "Performance";
        var description = "Response time degrades with context length";
        var confidence = 0.85;
        var evidence = new List<string> { "Benchmark A", "Benchmark B" };
        var discoveredAt = DateTime.UtcNow;

        // Act
        var insight = new Insight(category, description, confidence, evidence, discoveredAt);

        // Assert
        insight.Category.Should().Be(category);
        insight.Description.Should().Be(description);
        insight.Confidence.Should().Be(confidence);
        insight.SupportingEvidence.Should().BeEquivalentTo(evidence);
        insight.DiscoveredAt.Should().Be(discoveredAt);
    }

    [Fact]
    public void Constructor_WithEmptyEvidence_Succeeds()
    {
        var insight = new Insight("cat", "desc", 0.5, new List<string>(), DateTime.UtcNow);
        insight.SupportingEvidence.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = DateTime.UtcNow;
        var evidence = new List<string> { "e" };

        var a = new Insight("cat", "desc", 0.5, evidence, time);
        var b = new Insight("cat", "desc", 0.5, evidence, time);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentCategory_AreNotEqual()
    {
        var time = DateTime.UtcNow;
        var evidence = new List<string>();

        var a = new Insight("cat1", "desc", 0.5, evidence, time);
        var b = new Insight("cat2", "desc", 0.5, evidence, time);

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new Insight("cat", "desc", 0.5, new List<string>(), DateTime.UtcNow);

        var modified = original with { Confidence = 0.95 };

        modified.Confidence.Should().Be(0.95);
        modified.Category.Should().Be(original.Category);
    }
}
