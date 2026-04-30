// <copyright file="ConsolidatedActionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Tests.NanoAtoms;

[Trait("Category", "Unit")]
public sealed class ConsolidatedActionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var digests = new List<DigestFragment>();
        var timestamp = DateTime.UtcNow;

        // Act
        var action = new ConsolidatedAction(
            id, "response content", digests, 0.85, "response", 3, 150, timestamp);

        // Assert
        action.Id.Should().Be(id);
        action.Content.Should().Be("response content");
        action.SourceDigests.Should().BeSameAs(digests);
        action.Confidence.Should().Be(0.85);
        action.ActionType.Should().Be("response");
        action.StreamCount.Should().Be(3);
        action.ElapsedMs.Should().Be(150);
        action.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var digests = new List<DigestFragment>();
        var timestamp = DateTime.UtcNow;

        var a = new ConsolidatedAction(id, "content", digests, 0.9, "plan", 2, 100, timestamp);
        var b = new ConsolidatedAction(id, "content", digests, 0.9, "plan", 2, 100, timestamp);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var action = new ConsolidatedAction(
            Guid.NewGuid(), "original", new List<DigestFragment>(), 0.5, "response", 1, 50, DateTime.UtcNow);

        // Act
        var modified = action with { Content = "updated", Confidence = 0.95 };

        // Assert
        modified.Content.Should().Be("updated");
        modified.Confidence.Should().Be(0.95);
        modified.ActionType.Should().Be("response");
    }
}
