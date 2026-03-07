// <copyright file="VectorStoreDocumentRecordTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using LangChain.Databases;
using Microsoft.Extensions.VectorData;
using Ouroboros.SemanticKernel.VectorData;

namespace Ouroboros.SemanticKernel.Tests.VectorData;

public sealed class VectorStoreDocumentRecordTests
{
    // ── Property getters / setters ────────────────────────────────────────

    [Fact]
    public void Id_DefaultValue_IsEmptyString()
    {
        var record = new VectorStoreDocumentRecord();
        record.Id.Should().BeEmpty();
    }

    [Fact]
    public void Content_DefaultValue_IsEmptyString()
    {
        var record = new VectorStoreDocumentRecord();
        record.Content.Should().BeEmpty();
    }

    [Fact]
    public void Embedding_DefaultValue_IsEmptyMemory()
    {
        var record = new VectorStoreDocumentRecord();
        record.Embedding.Length.Should().Be(0);
    }

    [Fact]
    public void Properties_SetAndGet_Correctly()
    {
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        var record = new VectorStoreDocumentRecord
        {
            Id = "test-id",
            Content = "test-content",
            Embedding = embedding,
        };

        record.Id.Should().Be("test-id");
        record.Content.Should().Be("test-content");
        record.Embedding.ToArray().Should().BeEquivalentTo(new[] { 0.1f, 0.2f, 0.3f });
    }

    // ── FromLangChainVector ───────────────────────────────────────────────

    [Fact]
    public void FromLangChainVector_NullVector_ThrowsArgumentNullException()
    {
        var act = () => VectorStoreDocumentRecord.FromLangChainVector(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("vector");
    }

    [Fact]
    public void FromLangChainVector_WithAllFields_MapsCorrectly()
    {
        var embedding = new float[] { 1.0f, 2.0f };
        var vector = new Vector
        {
            Id = "vec-1",
            Text = "hello world",
            Embedding = embedding,
        };

        var record = VectorStoreDocumentRecord.FromLangChainVector(vector);

        record.Id.Should().Be("vec-1");
        record.Content.Should().Be("hello world");
        record.Embedding.ToArray().Should().BeEquivalentTo(embedding);
    }

    [Fact]
    public void FromLangChainVector_NullId_GeneratesGuid()
    {
        var vector = new Vector { Id = null, Text = "content" };

        var record = VectorStoreDocumentRecord.FromLangChainVector(vector);

        record.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(record.Id, out _).Should().BeTrue("a GUID should be generated for null ID");
    }

    [Fact]
    public void FromLangChainVector_NullText_DefaultsToEmpty()
    {
        var vector = new Vector { Id = "id", Text = null };

        var record = VectorStoreDocumentRecord.FromLangChainVector(vector);

        record.Content.Should().BeEmpty();
    }

    [Fact]
    public void FromLangChainVector_NullEmbedding_DefaultsToEmpty()
    {
        var vector = new Vector { Id = "id", Text = "text", Embedding = null };

        var record = VectorStoreDocumentRecord.FromLangChainVector(vector);

        record.Embedding.Length.Should().Be(0);
    }

    // ── ToDocument ────────────────────────────────────────────────────────

    [Fact]
    public void ToDocument_ReturnsDocumentWithContentAndIdMetadata()
    {
        var record = new VectorStoreDocumentRecord
        {
            Id = "doc-42",
            Content = "some document content",
        };

        var doc = record.ToDocument();

        doc.PageContent.Should().Be("some document content");
        doc.Metadata.Should().ContainKey("id");
        doc.Metadata["id"].Should().Be("doc-42");
    }

    [Fact]
    public void ToDocument_EmptyRecord_ReturnsDocumentWithEmptyContent()
    {
        var record = new VectorStoreDocumentRecord();

        var doc = record.ToDocument();

        doc.PageContent.Should().BeEmpty();
        doc.Metadata["id"].Should().Be(string.Empty);
    }

    // ── BuildDefinition ──────────────────────────────────────────────────

    [Fact]
    public void BuildDefinition_ReturnsDefinitionWithThreeProperties()
    {
        var definition = VectorStoreDocumentRecord.BuildDefinition(1536);

        definition.Properties.Should().HaveCount(3);
    }

    [Fact]
    public void BuildDefinition_ContainsKeyProperty()
    {
        var definition = VectorStoreDocumentRecord.BuildDefinition(768);

        definition.Properties.Should().Contain(p =>
            p is VectorStoreKeyProperty && p.Name == "Id");
    }

    [Fact]
    public void BuildDefinition_ContainsDataProperty()
    {
        var definition = VectorStoreDocumentRecord.BuildDefinition(768);

        definition.Properties.Should().Contain(p =>
            p is VectorStoreDataProperty && p.Name == "Content");
    }

    [Fact]
    public void BuildDefinition_ContainsVectorPropertyWithCorrectDimension()
    {
        const int dimension = 384;
        var definition = VectorStoreDocumentRecord.BuildDefinition(dimension);

        var vectorProp = definition.Properties
            .OfType<VectorStoreVectorProperty>()
            .Single();

        vectorProp.Name.Should().Be("Embedding");
        vectorProp.Dimensions.Should().Be(dimension);
    }

    // ── Roundtrip ────────────────────────────────────────────────────────

    [Fact]
    public void Roundtrip_VectorToRecordToDocument_PreservesContent()
    {
        var vector = new Vector
        {
            Id = "rt-1",
            Text = "roundtrip content",
            Embedding = new float[] { 0.5f },
        };

        var record = VectorStoreDocumentRecord.FromLangChainVector(vector);
        var doc = record.ToDocument();

        doc.PageContent.Should().Be("roundtrip content");
        doc.Metadata["id"].Should().Be("rt-1");
    }
}
