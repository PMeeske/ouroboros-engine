// <copyright file="ResearchPaperTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ResearchPaperTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = "2301.12345";
        var title = "Attention Is All You Need";
        var authors = "Vaswani, Ashish";
        var abstractText = "We propose the Transformer architecture...";
        var category = "cs.CL";
        var url = "https://arxiv.org/abs/2301.12345";
        var publishedDate = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var paper = new ResearchPaper(id, title, authors, abstractText, category, url, publishedDate);

        // Assert
        paper.Id.Should().Be(id);
        paper.Title.Should().Be(title);
        paper.Authors.Should().Be(authors);
        paper.Abstract.Should().Be(abstractText);
        paper.Category.Should().Be(category);
        paper.Url.Should().Be(url);
        paper.PublishedDate.Should().Be(publishedDate);
    }

    [Fact]
    public void Constructor_PublishedDateDefaultsToNull()
    {
        var paper = new ResearchPaper("id", "title", "authors", "abstract", "cs.AI", "url");

        paper.PublishedDate.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithExplicitNullDate_SetsNull()
    {
        var paper = new ResearchPaper("id", "title", "authors", "abstract", "cs.AI", "url", null);

        paper.PublishedDate.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ResearchPaper("id", "title", "authors", "abstract", "cs.AI", "url");
        var b = new ResearchPaper("id", "title", "authors", "abstract", "cs.AI", "url");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var a = new ResearchPaper("id1", "title", "authors", "abstract", "cs.AI", "url");
        var b = new ResearchPaper("id2", "title", "authors", "abstract", "cs.AI", "url");

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new ResearchPaper("id", "title", "authors", "abstract", "cs.AI", "url");

        var modified = original with { Title = "Updated title" };

        modified.Title.Should().Be("Updated title");
        modified.Id.Should().Be(original.Id);
    }
}
