// <copyright file="OuroborosAtomTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Tests for OuroborosAtom functionality.
/// </summary>
[Trait("Category", "Unit")]
public class OuroborosAtomTests
{
    [Fact]
    public void GetStrategyWeight_WithNoCapabilities_ReturnsDefaultValue()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();

        // Act
        double weight = atom.GetStrategyWeight("ToolVsLLMWeight", 0.7);

        // Assert
        weight.Should().Be(0.7);
    }

    [Fact]
    public void GetStrategyWeight_WithMatchingCapability_ReturnsConfidenceLevel()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tool vs LLM routing weight", 0.85));

        // Act
        double weight = atom.GetStrategyWeight("ToolVsLLMWeight", 0.7);

        // Assert
        weight.Should().Be(0.85);
    }

    [Fact]
    public void GetStrategyWeight_WithNonMatchingCapability_ReturnsDefaultValue()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "Planning depth", 0.6));

        // Act
        double weight = atom.GetStrategyWeight("ToolVsLLMWeight", 0.7);

        // Assert
        weight.Should().Be(0.7);
    }

    [Fact]
    public void GetStrategyWeight_WithEmptyStrategyName_ReturnsDefaultValue()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tool vs LLM routing weight", 0.85));

        // Act
        double weight = atom.GetStrategyWeight("", 0.5);

        // Assert
        weight.Should().Be(0.5);
    }

    [Fact]
    public void GetStrategyWeight_IsCaseInsensitive()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tool vs LLM routing weight", 0.9));

        // Act
        double weight = atom.GetStrategyWeight("toolvsllmweight", 0.5);

        // Assert
        weight.Should().Be(0.9);
    }

    [Fact]
    public void GetStrategyWeight_WithMultipleStrategies_ReturnsCorrectOne()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "Planning depth", 0.6));
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tool vs LLM routing weight", 0.8));
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Verification strictness", 0.7));

        // Act
        double toolWeight = atom.GetStrategyWeight("ToolVsLLMWeight", 0.5);
        double planningWeight = atom.GetStrategyWeight("PlanningDepth", 0.5);
        double verificationWeight = atom.GetStrategyWeight("VerificationStrictness", 0.5);

        // Assert
        toolWeight.Should().Be(0.8);
        planningWeight.Should().Be(0.6);
        verificationWeight.Should().Be(0.7);
    }

    [Fact]
    public void GetStrategyWeight_UpdatedCapability_ReturnsNewValue()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tool vs LLM routing weight", 0.6));

        // Act - Get initial value
        double initialWeight = atom.GetStrategyWeight("ToolVsLLMWeight", 0.5);

        // Update capability
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tool vs LLM routing weight", 0.9));

        // Act - Get updated value
        double updatedWeight = atom.GetStrategyWeight("ToolVsLLMWeight", 0.5);

        // Assert
        initialWeight.Should().Be(0.6);
        updatedWeight.Should().Be(0.9);
    }
}
