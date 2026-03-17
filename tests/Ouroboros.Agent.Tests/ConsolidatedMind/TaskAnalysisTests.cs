// <copyright file="TaskAnalysisTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class TaskAnalysisTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var secondary = new[] { SpecializedRole.Analyst, SpecializedRole.Verifier };
        var capabilities = new[] { "code", "debug" };

        // Act
        var analysis = new TaskAnalysis(
            SpecializedRole.CodeExpert,
            secondary,
            capabilities,
            EstimatedComplexity: 0.75,
            RequiresThinking: true,
            RequiresVerification: true,
            Confidence: 0.9);

        // Assert
        analysis.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
        analysis.SecondaryRoles.Should().BeEquivalentTo(secondary);
        analysis.RequiredCapabilities.Should().BeEquivalentTo(capabilities);
        analysis.EstimatedComplexity.Should().Be(0.75);
        analysis.RequiresThinking.Should().BeTrue();
        analysis.RequiresVerification.Should().BeTrue();
        analysis.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void EmptyArrays_AreValid()
    {
        // Act
        var analysis = new TaskAnalysis(
            SpecializedRole.QuickResponse,
            Array.Empty<SpecializedRole>(),
            Array.Empty<string>(),
            0.0,
            false,
            false,
            1.0);

        // Assert
        analysis.SecondaryRoles.Should().BeEmpty();
        analysis.RequiredCapabilities.Should().BeEmpty();
    }
}
