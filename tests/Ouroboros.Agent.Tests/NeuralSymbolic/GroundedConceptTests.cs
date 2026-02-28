// <copyright file="GroundedConceptTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

/// <summary>
/// Unit tests for the <see cref="GroundedConcept"/> record.
/// Covers construction, property initialization, and record equality.
/// </summary>
[Trait("Category", "Unit")]
public class GroundedConceptTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var properties = new List<string> { "color", "size" };
        var relations = new List<string> { "is-a", "part-of" };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var concept = new GroundedConcept(
            "apple",
            "Fruit",
            properties,
            relations,
            embedding,
            0.95);

        // Assert
        concept.Name.Should().Be("apple");
        concept.MeTTaType.Should().Be("Fruit");
        concept.Properties.Should().HaveCount(2);
        concept.Relations.Should().HaveCount(2);
        concept.Embedding.Should().HaveCount(3);
        concept.GroundingConfidence.Should().Be(0.95);
    }

    [Fact]
    public void Constructor_EmptyCollections_IsValid()
    {
        // Act
        var concept = new GroundedConcept(
            "unknown",
            "Thing",
            new List<string>(),
            new List<string>(),
            Array.Empty<float>(),
            0.0);

        // Assert
        concept.Properties.Should().BeEmpty();
        concept.Relations.Should().BeEmpty();
        concept.Embedding.Should().BeEmpty();
        concept.GroundingConfidence.Should().Be(0.0);
    }

    [Fact]
    public void Constructor_HighDimensionalEmbedding_PreservesValues()
    {
        // Arrange
        var embedding = Enumerable.Range(0, 768).Select(i => (float)i * 0.001f).ToArray();

        // Act
        var concept = new GroundedConcept(
            "complex-concept",
            "AbstractEntity",
            new List<string>(),
            new List<string>(),
            embedding,
            0.75);

        // Assert
        concept.Embedding.Should().HaveCount(768);
        concept.Embedding[0].Should().Be(0.0f);
        concept.Embedding[767].Should().BeApproximately(0.767f, 0.001f);
    }

    [Fact]
    public void Constructor_ZeroConfidence_IsAllowed()
    {
        // Act
        var concept = new GroundedConcept(
            "ungrounded",
            "Unknown",
            new List<string>(),
            new List<string>(),
            new float[] { 0.5f },
            0.0);

        // Assert
        concept.GroundingConfidence.Should().Be(0.0);
    }

    [Fact]
    public void Constructor_FullConfidence_IsAllowed()
    {
        // Act
        var concept = new GroundedConcept(
            "certain",
            "Known",
            new List<string> { "verified" },
            new List<string>(),
            new float[] { 1.0f },
            1.0);

        // Assert
        concept.GroundingConfidence.Should().Be(1.0);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var props = new List<string> { "p1" };
        var rels = new List<string> { "r1" };
        var emb = new float[] { 0.1f, 0.2f };

        var a = new GroundedConcept("c", "T", props, rels, emb, 0.5);
        var b = new GroundedConcept("c", "T", props, rels, emb, 0.5);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentName_AreNotEqual()
    {
        // Arrange
        var props = new List<string>();
        var rels = new List<string>();
        var emb = new float[] { 0.1f };

        var a = new GroundedConcept("cat", "Animal", props, rels, emb, 0.8);
        var b = new GroundedConcept("dog", "Animal", props, rels, emb, 0.8);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentMeTTaType_AreNotEqual()
    {
        // Arrange
        var props = new List<string>();
        var rels = new List<string>();
        var emb = new float[] { 0.1f };

        var a = new GroundedConcept("item", "Fruit", props, rels, emb, 0.8);
        var b = new GroundedConcept("item", "Vegetable", props, rels, emb, 0.8);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentConfidence_AreNotEqual()
    {
        // Arrange
        var props = new List<string>();
        var rels = new List<string>();
        var emb = new float[] { 0.1f };

        var a = new GroundedConcept("item", "Type", props, rels, emb, 0.5);
        var b = new GroundedConcept("item", "Type", props, rels, emb, 0.9);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Constructor_MultiplePropertiesAndRelations_PreservesOrder()
    {
        // Arrange
        var properties = new List<string> { "red", "round", "sweet" };
        var relations = new List<string> { "grows-on-tree", "is-food", "has-seeds" };

        // Act
        var concept = new GroundedConcept(
            "apple",
            "Fruit",
            properties,
            relations,
            new float[] { 0.5f },
            0.9);

        // Assert
        concept.Properties.Should().ContainInOrder("red", "round", "sweet");
        concept.Relations.Should().ContainInOrder("grows-on-tree", "is-food", "has-seeds");
    }
}
