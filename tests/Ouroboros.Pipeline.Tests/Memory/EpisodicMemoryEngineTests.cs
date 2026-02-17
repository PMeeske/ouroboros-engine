// <copyright file="EpisodicMemoryEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Pipeline.Memory;

using FluentAssertions;
using Ouroboros.Pipeline.Memory;
using Xunit;

/// <summary>
/// Unit tests for EpisodicMemoryEngine.
/// Tests construction, validation, and Result monad patterns.
/// Integration tests with real Qdrant are in separate test class.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodicMemoryEngineTests
{
    [Fact]
    public void Constructor_WithConnectionString_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var embedding = new TestEmbeddingModel();

        // Act
        var engine = new EpisodicMemoryEngine(connectionString, embedding);

        // Assert
        engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        string? connectionString = null;
        var embedding = new TestEmbeddingModel();

        // Act
        Action act = () => new EpisodicMemoryEngine(connectionString!, embedding);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var connectionString = string.Empty;
        var embedding = new TestEmbeddingModel();

        // Act
        Action act = () => new EpisodicMemoryEngine(connectionString, embedding);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithCustomCollectionName_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var embedding = new TestEmbeddingModel();
        var customCollection = "custom_episodes";

        // Act
        var engine = new EpisodicMemoryEngine(connectionString, embedding, customCollection);

        // Assert
        engine.Should().NotBeNull();
    }

    /// <summary>
    /// Test implementation of IEmbeddingModel.
    /// </summary>
    private class TestEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            // Return a dummy embedding vector
            var embedding = new float[768];
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = 0.1f;
            }

            return Task.FromResult(embedding);
        }
    }
}