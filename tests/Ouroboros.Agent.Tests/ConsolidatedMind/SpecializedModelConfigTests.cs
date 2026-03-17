// <copyright file="SpecializedModelConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class SpecializedModelConfigTests
{
    [Fact]
    public void Constructor_WithRequiredOnly_SetsDefaults()
    {
        // Act
        var config = new SpecializedModelConfig(SpecializedRole.CodeExpert, "codellama:34b");

        // Assert
        config.Role.Should().Be(SpecializedRole.CodeExpert);
        config.OllamaModel.Should().Be("codellama:34b");
        config.Endpoint.Should().BeNull();
        config.Capabilities.Should().BeNull();
        config.Priority.Should().Be(1.0);
        config.MaxTokens.Should().Be(4096);
        config.Temperature.Should().Be(0.7);
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAll()
    {
        // Arrange
        var caps = new[] { "code", "debug" };

        // Act
        var config = new SpecializedModelConfig(
            SpecializedRole.Creative,
            "llama3.1:70b",
            Endpoint: "http://custom:11434",
            Capabilities: caps,
            Priority: 0.9,
            MaxTokens: 8192,
            Temperature: 0.9);

        // Assert
        config.Role.Should().Be(SpecializedRole.Creative);
        config.OllamaModel.Should().Be("llama3.1:70b");
        config.Endpoint.Should().Be("http://custom:11434");
        config.Capabilities.Should().BeEquivalentTo(caps);
        config.Priority.Should().Be(0.9);
        config.MaxTokens.Should().Be(8192);
        config.Temperature.Should().Be(0.9);
    }

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        // Arrange
        var c1 = new SpecializedModelConfig(SpecializedRole.Planner, "model-a");
        var c2 = new SpecializedModelConfig(SpecializedRole.Planner, "model-a");

        // Assert
        c1.Should().Be(c2);
    }

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new SpecializedModelConfig(SpecializedRole.Analyst, "model-x", Temperature: 0.5);

        // Act
        var modified = original with { Temperature = 0.9 };

        // Assert
        modified.Temperature.Should().Be(0.9);
        modified.Role.Should().Be(original.Role);
        modified.OllamaModel.Should().Be(original.OllamaModel);
    }
}
