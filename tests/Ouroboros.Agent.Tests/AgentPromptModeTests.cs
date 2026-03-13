// <copyright file="AgentPromptModeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for the <see cref="AgentPromptMode"/> enum.
/// Covers value definitions, count, and ordinal stability.
/// </summary>
[Trait("Category", "Unit")]
public class AgentPromptModeTests
{
    [Theory]
    [InlineData(AgentPromptMode.Standard)]
    [InlineData(AgentPromptMode.SelfCritique)]
    [InlineData(AgentPromptMode.Ouroboros)]
    public void AllValues_AreDefined(AgentPromptMode mode)
    {
        // Assert
        Enum.IsDefined(mode).Should().BeTrue();
    }

    [Fact]
    public void HasThreeValues()
    {
        // Assert
        Enum.GetValues<AgentPromptMode>().Should().HaveCount(3);
    }

    [Fact]
    public void Standard_IsDefaultValue()
    {
        // Arrange
        var defaultMode = default(AgentPromptMode);

        // Assert
        defaultMode.Should().Be(AgentPromptMode.Standard);
    }

    [Theory]
    [InlineData(AgentPromptMode.Standard, 0)]
    [InlineData(AgentPromptMode.SelfCritique, 1)]
    [InlineData(AgentPromptMode.Ouroboros, 2)]
    public void OrdinalValues_AreStable(AgentPromptMode mode, int expectedOrdinal)
    {
        // Assert
        ((int)mode).Should().Be(expectedOrdinal);
    }

    [Fact]
    public void ToString_ReturnsExpectedNames()
    {
        // Assert
        AgentPromptMode.Standard.ToString().Should().Be("Standard");
        AgentPromptMode.SelfCritique.ToString().Should().Be("SelfCritique");
        AgentPromptMode.Ouroboros.ToString().Should().Be("Ouroboros");
    }

    [Fact]
    public void Parse_ValidString_ReturnsCorrectValue()
    {
        // Act & Assert
        Enum.Parse<AgentPromptMode>("Standard").Should().Be(AgentPromptMode.Standard);
        Enum.Parse<AgentPromptMode>("SelfCritique").Should().Be(AgentPromptMode.SelfCritique);
        Enum.Parse<AgentPromptMode>("Ouroboros").Should().Be(AgentPromptMode.Ouroboros);
    }

    [Fact]
    public void Parse_InvalidString_ThrowsArgumentException()
    {
        // Act
        var act = () => Enum.Parse<AgentPromptMode>("InvalidMode");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
