// <copyright file="ConfiguredMindArrowSystemTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

/// <summary>
/// Tests for <see cref="ConsolidatedMindArrowsExtensions"/> public arrow factory methods.
/// ConfiguredMindArrowSystem has an internal constructor so we test the public factory methods
/// that create arrow steps directly.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConsolidatedMindArrowsExtensionsTests
{
    private readonly Mock<IChatCompletionModel> _mockModel;
    private readonly SpecializedModel[] _specialists;
    private readonly MindConfig _config;

    public ConsolidatedMindArrowsExtensionsTests()
    {
        _mockModel = new Mock<IChatCompletionModel>();
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test response");

        _specialists = new[]
        {
            new SpecializedModel(
                SpecializedRole.QuickResponse, _mockModel.Object, "test-model", new[] { "general" }),
            new SpecializedModel(
                SpecializedRole.CodeExpert, _mockModel.Object, "code-model", new[] { "code" }),
        };

        _config = MindConfig.Minimal();
    }

    [Fact]
    public void CreateProcessingArrowFactory_ReturnsNonNullFactory()
    {
        // Act
        var factory = ConsolidatedMindArrowsExtensions.CreateProcessingArrowFactory(
            _specialists, _config);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void CreateProcessingArrowFactory_FactoryProducesStep()
    {
        // Arrange
        var factory = ConsolidatedMindArrowsExtensions.CreateProcessingArrowFactory(
            _specialists, _config);

        // Act
        var step = factory("test prompt");

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void ReasoningArrowWithExplicitConfig_ReturnsStep()
    {
        // Arrange
        var embedMock = new Mock<IEmbeddingModel>();

        // Act
        var step = ConsolidatedMindArrowsExtensions.ReasoningArrowWithExplicitConfig(
            _specialists, _config, embedMock.Object, "topic", "query");

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void SafeReasoningArrowWithExplicitConfig_ReturnsArrow()
    {
        // Arrange
        var embedMock = new Mock<IEmbeddingModel>();

        // Act
        var arrow = ConsolidatedMindArrowsExtensions.SafeReasoningArrowWithExplicitConfig(
            _specialists, _config, embedMock.Object, "topic", "query");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void ComplexTaskArrowWithExplicitConfig_ReturnsStep()
    {
        // Arrange
        var embedMock = new Mock<IEmbeddingModel>();

        // Act
        var step = ConsolidatedMindArrowsExtensions.ComplexTaskArrowWithExplicitConfig(
            _specialists, _config, embedMock.Object, "complex task");

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public void CreateProcessingArrowFactory_WithTools_ReturnsFactory()
    {
        // Arrange
        var tools = new ToolRegistry();

        // Act
        var factory = ConsolidatedMindArrowsExtensions.CreateProcessingArrowFactory(
            _specialists, _config, tools);

        // Assert
        factory.Should().NotBeNull();
    }
}
