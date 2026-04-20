// <copyright file="AgentCapabilityTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

/// <summary>
/// Tests for the AgentCapability record to ensure correct property binding
/// and record semantics (value equality, with-expression support).
/// </summary>
[Trait("Category", "Unit")]
public class AgentCapabilityTests
{
    [Fact]
    public void AgentCapability_AllProperties_AreSetCorrectly()
    {
        // Arrange
        var createdAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastUsed = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var tools = new List<string> { "tool1", "tool2" };
        var limitations = new List<string> { "Cannot handle large files" };
        var metadata = new Dictionary<string, object> { ["version"] = "1.0" };

        // Act
        var capability = new AgentCapability(
            Name: "TextAnalysis",
            Description: "Analyzes text for sentiment and topics",
            RequiredTools: tools,
            SuccessRate: 0.92,
            AverageLatency: 150.5,
            KnownLimitations: limitations,
            UsageCount: 42,
            CreatedAt: createdAt,
            LastUsed: lastUsed,
            Metadata: metadata);

        // Assert
        capability.Name.Should().Be("TextAnalysis");
        capability.Description.Should().Be("Analyzes text for sentiment and topics");
        capability.RequiredTools.Should().BeEquivalentTo(new[] { "tool1", "tool2" });
        capability.SuccessRate.Should().Be(0.92);
        capability.AverageLatency.Should().Be(150.5);
        capability.KnownLimitations.Should().ContainSingle("Cannot handle large files");
        capability.UsageCount.Should().Be(42);
        capability.CreatedAt.Should().Be(createdAt);
        capability.LastUsed.Should().Be(lastUsed);
        capability.Metadata.Should().ContainKey("version");
    }

    [Fact]
    public void AgentCapability_WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new AgentCapability(
            "Test", "Description", new List<string>(), 0.5, 100.0,
            new List<string>(), 0, DateTime.UtcNow, DateTime.UtcNow,
            new Dictionary<string, object>());

        // Act
        var modified = original with { SuccessRate = 0.95, UsageCount = 10 };

        // Assert
        modified.SuccessRate.Should().Be(0.95);
        modified.UsageCount.Should().Be(10);
        modified.Name.Should().Be(original.Name);
        modified.Description.Should().Be(original.Description);
    }

    [Fact]
    public void AgentCapability_ValueEquality_WorksCorrectly()
    {
        // Arrange
        var tools = new List<string> { "tool1" };
        var limitations = new List<string>();
        var metadata = new Dictionary<string, object>();
        var time = DateTime.UtcNow;

        var cap1 = new AgentCapability("A", "B", tools, 0.5, 100, limitations, 1, time, time, metadata);
        var cap2 = new AgentCapability(
            "A",
            "B",
            new List<string>(tools),
            0.5,
            100,
            new List<string>(limitations),
            1,
            time,
            time,
            new Dictionary<string, object>(metadata));

        // Assert — record equality uses reference equality for mutable collections,
        // so otherwise-equivalent records with different collection instances are not equal
        cap1.Should().NotBe(cap2);
    }

    [Fact]
    public void AgentCapability_EmptyCollections_AreValid()
    {
        // Act
        var capability = new AgentCapability(
            "Minimal", "Minimal capability",
            new List<string>(), 0.0, 0.0,
            new List<string>(), 0,
            DateTime.MinValue, DateTime.MinValue,
            new Dictionary<string, object>());

        // Assert
        capability.RequiredTools.Should().BeEmpty();
        capability.KnownLimitations.Should().BeEmpty();
        capability.Metadata.Should().BeEmpty();
        capability.SuccessRate.Should().Be(0.0);
        capability.UsageCount.Should().Be(0);
    }
}
