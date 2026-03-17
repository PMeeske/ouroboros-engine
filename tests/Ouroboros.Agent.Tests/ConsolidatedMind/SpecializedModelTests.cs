// <copyright file="SpecializedModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class SpecializedModelTests
{
    private static SpecializedModel CreateModel(
        SpecializedRole role = SpecializedRole.CodeExpert,
        string[] capabilities = null!,
        double priority = 1.0)
    {
        var mockModel = new Mock<IChatCompletionModel>();
        return new SpecializedModel(
            role,
            mockModel.Object,
            "test-model",
            capabilities ?? new[] { "code", "debug", "refactor" },
            Priority: priority);
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var mockModel = new Mock<IChatCompletionModel>();

        // Act
        var specialist = new SpecializedModel(
            SpecializedRole.DeepReasoning,
            mockModel.Object,
            "reasoning-model",
            new[] { "logic", "analysis" },
            Priority: 0.9,
            MaxTokens: 8192,
            CostPerToken: 0.001,
            AverageLatencyMs: 1000.0);

        // Assert
        specialist.Role.Should().Be(SpecializedRole.DeepReasoning);
        specialist.Model.Should().BeSameAs(mockModel.Object);
        specialist.ModelName.Should().Be("reasoning-model");
        specialist.Capabilities.Should().BeEquivalentTo(new[] { "logic", "analysis" });
        specialist.Priority.Should().Be(0.9);
        specialist.MaxTokens.Should().Be(8192);
        specialist.CostPerToken.Should().Be(0.001);
        specialist.AverageLatencyMs.Should().Be(1000.0);
    }

    [Fact]
    public void Constructor_DefaultValues_AreCorrect()
    {
        // Arrange
        var mockModel = new Mock<IChatCompletionModel>();

        // Act
        var specialist = new SpecializedModel(
            SpecializedRole.QuickResponse,
            mockModel.Object,
            "quick-model",
            new[] { "fast" });

        // Assert
        specialist.Priority.Should().Be(1.0);
        specialist.MaxTokens.Should().Be(4096);
        specialist.CostPerToken.Should().Be(0.0);
        specialist.AverageLatencyMs.Should().Be(500.0);
    }

    [Fact]
    public void CalculateFitness_WithMatchingCapabilities_ReturnsHighScore()
    {
        // Arrange
        var model = CreateModel(capabilities: new[] { "code", "debug", "refactor" });
        var taskCapabilities = new[] { "code", "debug" };

        // Act
        double fitness = model.CalculateFitness(taskCapabilities);

        // Assert
        fitness.Should().Be(1.0); // 2/2 matches * 1.0 priority
    }

    [Fact]
    public void CalculateFitness_WithPartialMatch_ReturnsPartialScore()
    {
        // Arrange
        var model = CreateModel(capabilities: new[] { "code", "debug" });
        var taskCapabilities = new[] { "code", "math", "logic" };

        // Act
        double fitness = model.CalculateFitness(taskCapabilities);

        // Assert
        fitness.Should().BeApproximately(1.0 / 3.0, 0.001); // 1/3 matches * 1.0 priority
    }

    [Fact]
    public void CalculateFitness_WithNoMatch_ReturnsZero()
    {
        // Arrange
        var model = CreateModel(capabilities: new[] { "creative", "writing" });
        var taskCapabilities = new[] { "code", "math" };

        // Act
        double fitness = model.CalculateFitness(taskCapabilities);

        // Assert
        fitness.Should().Be(0.0);
    }

    [Fact]
    public void CalculateFitness_WithNullCapabilities_ReturnsDefaultScore()
    {
        // Arrange
        var model = CreateModel();

        // Act
        double fitness = model.CalculateFitness(null!);

        // Assert
        fitness.Should().Be(0.5);
    }

    [Fact]
    public void CalculateFitness_WithEmptyCapabilities_ReturnsDefaultScore()
    {
        // Arrange
        var model = CreateModel();

        // Act
        double fitness = model.CalculateFitness(Array.Empty<string>());

        // Assert
        fitness.Should().Be(0.5);
    }

    [Fact]
    public void CalculateFitness_RespectsCapabilityCaseInsensitivity()
    {
        // Arrange
        var model = CreateModel(capabilities: new[] { "CODE", "Debug" });
        var taskCapabilities = new[] { "code", "debug" };

        // Act
        double fitness = model.CalculateFitness(taskCapabilities);

        // Assert
        fitness.Should().Be(1.0);
    }

    [Fact]
    public void CalculateFitness_WithPriority_ScalesScore()
    {
        // Arrange
        var model = CreateModel(
            capabilities: new[] { "code", "debug" },
            priority: 0.5);
        var taskCapabilities = new[] { "code", "debug" };

        // Act
        double fitness = model.CalculateFitness(taskCapabilities);

        // Assert
        fitness.Should().Be(0.5); // 2/2 matches * 0.5 priority
    }
}
