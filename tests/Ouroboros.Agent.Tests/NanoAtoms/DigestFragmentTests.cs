// <copyright file="DigestFragmentTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Tests.NanoAtoms;

[Trait("Category", "Unit")]
public sealed class DigestFragmentTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        // Act
        var fragment = new DigestFragment(
            id, sourceId, "compressed thought", 2.5, 0.9, NanoAtomPhase.Digest, timestamp);

        // Assert
        fragment.Id.Should().Be(id);
        fragment.SourceAtomId.Should().Be(sourceId);
        fragment.Content.Should().Be("compressed thought");
        fragment.CompressionRatio.Should().Be(2.5);
        fragment.Confidence.Should().Be(0.9);
        fragment.CompletedPhase.Should().Be(NanoAtomPhase.Digest);
        fragment.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var a = new DigestFragment(id, sourceId, "content", 1.5, 0.8, NanoAtomPhase.Emit, timestamp);
        var b = new DigestFragment(id, sourceId, "content", 1.5, 0.8, NanoAtomPhase.Emit, timestamp);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var fragment = new DigestFragment(
            Guid.NewGuid(), Guid.NewGuid(), "original", 1.0, 0.5, NanoAtomPhase.Process, DateTime.UtcNow);

        // Act
        var modified = fragment with { Content = "updated", Confidence = 0.99 };

        // Assert
        modified.Content.Should().Be("updated");
        modified.Confidence.Should().Be(0.99);
        modified.CompletedPhase.Should().Be(NanoAtomPhase.Process);
    }
}
