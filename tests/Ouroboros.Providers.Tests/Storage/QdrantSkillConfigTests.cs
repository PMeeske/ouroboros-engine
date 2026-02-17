// <copyright file="QdrantSkillRegistryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.Storage;

using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

/// <summary>
/// Unit tests for QdrantSkillConfig record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class QdrantSkillConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedValues()
    {
        // Act
        var config = new QdrantSkillConfig();

        // Assert
        config.ConnectionString.Should().Be("http://localhost:6334");
        config.CollectionName.Should().Be("ouroboros_skills");
        config.AutoSave.Should().BeTrue();
        config.VectorSize.Should().Be(1536);
    }

    [Fact]
    public void Config_WithCustomValues_SetsCorrectly()
    {
        // Act
        var config = new QdrantSkillConfig(
            ConnectionString: "http://qdrant.example.com:6333",
            CollectionName: "custom_skills",
            AutoSave: false,
            VectorSize: 768);

        // Assert
        config.ConnectionString.Should().Be("http://qdrant.example.com:6333");
        config.CollectionName.Should().Be("custom_skills");
        config.AutoSave.Should().BeFalse();
        config.VectorSize.Should().Be(768);
    }

    [Theory]
    [InlineData(384)]    // Small embedding models
    [InlineData(768)]    // BERT-based models
    [InlineData(1024)]   // Some transformer models
    [InlineData(1536)]   // OpenAI Ada
    [InlineData(4096)]   // Large models
    public void Config_WithDifferentVectorSizes_Accepted(int vectorSize)
    {
        // Act
        var config = new QdrantSkillConfig(VectorSize: vectorSize);

        // Assert
        config.VectorSize.Should().Be(vectorSize);
    }

    [Fact]
    public void Config_Equality_WorksCorrectly()
    {
        // Arrange
        var config1 = new QdrantSkillConfig(
            "http://localhost:6334",
            "skills",
            true,
            1536);
        var config2 = new QdrantSkillConfig(
            "http://localhost:6334",
            "skills",
            true,
            1536);

        // Assert
        config1.Should().Be(config2);
    }

    [Fact]
    public void Config_WithExpression_CreatesNewConfig()
    {
        // Arrange
        var original = new QdrantSkillConfig();

        // Act
        var modified = original with { AutoSave = false };

        // Assert
        original.AutoSave.Should().BeTrue();
        modified.AutoSave.Should().BeFalse();
        modified.ConnectionString.Should().Be(original.ConnectionString);
    }
}