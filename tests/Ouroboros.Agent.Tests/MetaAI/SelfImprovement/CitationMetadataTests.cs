// <copyright file="CitationMetadataTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CitationMetadataTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var paperId = "2301.12345";
        var title = "Attention Is All You Need";
        var citationCount = 50000;
        var influentialCount = 5000;
        var references = new List<string> { "ref1", "ref2" };
        var citedBy = new List<string> { "cite1", "cite2", "cite3" };

        // Act
        var metadata = new CitationMetadata(paperId, title, citationCount, influentialCount, references, citedBy);

        // Assert
        metadata.PaperId.Should().Be(paperId);
        metadata.Title.Should().Be(title);
        metadata.CitationCount.Should().Be(citationCount);
        metadata.InfluentialCitationCount.Should().Be(influentialCount);
        metadata.References.Should().BeEquivalentTo(references);
        metadata.CitedBy.Should().BeEquivalentTo(citedBy);
    }

    [Fact]
    public void Constructor_WithEmptyLists_Succeeds()
    {
        var metadata = new CitationMetadata("id", "title", 0, 0, new List<string>(), new List<string>());

        metadata.References.Should().BeEmpty();
        metadata.CitedBy.Should().BeEmpty();
        metadata.CitationCount.Should().Be(0);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var refs = new List<string> { "a" };
        var cited = new List<string> { "b" };
        var a = new CitationMetadata("id", "title", 10, 5, refs, cited);
        var b = new CitationMetadata("id", "title", 10, 5, refs, cited);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentPaperId_AreNotEqual()
    {
        var refs = new List<string>();
        var cited = new List<string>();
        var a = new CitationMetadata("id1", "title", 10, 5, refs, cited);
        var b = new CitationMetadata("id2", "title", 10, 5, refs, cited);

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new CitationMetadata("id", "title", 10, 5, new List<string>(), new List<string>());

        var modified = original with { CitationCount = 100 };

        modified.CitationCount.Should().Be(100);
        modified.PaperId.Should().Be(original.PaperId);
    }
}
