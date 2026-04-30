// <copyright file="StatisticalAnalysisTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class StatisticalAnalysisTests
{
    [Fact]
    public void Constructor_ValidParams_CreatesRecord()
    {
        var analysis = new StatisticalAnalysis(0.5, true, "Significant");

        analysis.EffectSize.Should().Be(0.5);
        analysis.IsSignificant.Should().BeTrue();
        analysis.Interpretation.Should().Be("Significant");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new StatisticalAnalysis(0.5, true, "Significant");
        var b = new StatisticalAnalysis(0.5, true, "Significant");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new StatisticalAnalysis(0.5, true, "Significant");
        var b = new StatisticalAnalysis(0.3, false, "Not significant");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Deconstruct_ReturnsCorrectValues()
    {
        var analysis = new StatisticalAnalysis(0.75, false, "Moderate");
        var (effectSize, isSignificant, interpretation) = analysis;

        effectSize.Should().Be(0.75);
        isSignificant.Should().BeFalse();
        interpretation.Should().Be("Moderate");
    }
}
