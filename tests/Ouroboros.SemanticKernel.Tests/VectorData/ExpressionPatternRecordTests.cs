// <copyright file="ExpressionPatternRecordTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.VectorData;
using Ouroboros.SemanticKernel.VectorData;

namespace Ouroboros.SemanticKernel.Tests.VectorData;

public sealed class ExpressionPatternRecordTests
{
    // ── Default values ──────────────────────────────────────────────────

    [Fact]
    public void DefaultValues_NewInstance_HasExpectedDefaults()
    {
        var record = new ExpressionPatternRecord();

        record.Id.Should().BeEmpty();
        record.Pattern.Should().BeEmpty();
        record.Category.Should().BeEmpty();
        record.Fitness.Should().Be(0);
        record.Generation.Should().Be(0);
        record.Embedding.Length.Should().Be(0);
    }

    // ── Property getters / setters ──────────────────────────────────────

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        var record = new ExpressionPatternRecord
        {
            Id = "pattern-42",
            Pattern = "expr : term '+' term ;",
            Category = "rule",
            Fitness = 0.85,
            Generation = 7,
            Embedding = embedding,
        };

        record.Id.Should().Be("pattern-42");
        record.Pattern.Should().Be("expr : term '+' term ;");
        record.Category.Should().Be("rule");
        record.Fitness.Should().Be(0.85);
        record.Generation.Should().Be(7);
        record.Embedding.ToArray().Should().BeEquivalentTo(new[] { 0.1f, 0.2f, 0.3f });
    }

    // ── BuildDefinition ─────────────────────────────────────────────────

    [Fact]
    public void BuildDefinition_ReturnsCorrectPropertyCount()
    {
        var definition = ExpressionPatternRecord.BuildDefinition(1536);

        definition.Properties.Should().HaveCount(6);
    }

    [Fact]
    public void BuildDefinition_UsesSpecifiedVectorDimension()
    {
        const int dimension = 384;
        var definition = ExpressionPatternRecord.BuildDefinition(dimension);

        var vectorProp = definition.Properties
            .OfType<VectorStoreVectorProperty>()
            .Single();

        vectorProp.Dimensions.Should().Be(dimension);
    }

    [Fact]
    public void BuildDefinition_ContainsKeyProperty_Id()
    {
        var definition = ExpressionPatternRecord.BuildDefinition(768);

        definition.Properties.Should().Contain(p =>
            p is VectorStoreKeyProperty && p.Name == "Id");
    }

    [Fact]
    public void BuildDefinition_ContainsVectorProperty_Embedding()
    {
        var definition = ExpressionPatternRecord.BuildDefinition(1536);

        var vectorProp = definition.Properties
            .OfType<VectorStoreVectorProperty>()
            .Single();

        vectorProp.Name.Should().Be("Embedding");
    }
}
