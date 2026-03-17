// <copyright file="HypergridContextTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;

namespace Ouroboros.Agent.Tests.Cognition.Planning;

/// <summary>
/// Unit tests for <see cref="HypergridContext"/>, <see cref="DimensionalCoordinate"/>,
/// and <see cref="HypergridAnalysis"/>.
/// </summary>
[Trait("Category", "Unit")]
public class HypergridContextTests
{
    // --- HypergridContext ---

    [Fact]
    public void HypergridContext_Default_HasExpectedValues()
    {
        var ctx = HypergridContext.Default;

        ctx.Deadline.Should().BeNull();
        ctx.AvailableSkills.Should().BeEmpty();
        ctx.AvailableTools.Should().BeEmpty();
        ctx.RiskThreshold.Should().Be(0.7);
    }

    [Fact]
    public void HypergridContext_WithCustomValues_SetsProperties()
    {
        var deadline = DateTimeOffset.UtcNow.AddHours(2);
        var skills = new List<string> { "coding", "analysis" };
        var tools = new List<string> { "calculator", "browser" };

        var ctx = new HypergridContext(deadline, skills, tools, 0.5);

        ctx.Deadline.Should().Be(deadline);
        ctx.AvailableSkills.Should().HaveCount(2);
        ctx.AvailableTools.Should().HaveCount(2);
        ctx.RiskThreshold.Should().Be(0.5);
    }

    [Fact]
    public void HypergridContext_Default_IsSingleton()
    {
        var first = HypergridContext.Default;
        var second = HypergridContext.Default;

        first.Should().BeSameAs(second);
    }

    // --- DimensionalCoordinate ---

    [Fact]
    public void DimensionalCoordinate_Origin_IsAllZeros()
    {
        var origin = DimensionalCoordinate.Origin;

        origin.Temporal.Should().Be(0);
        origin.Semantic.Should().Be(0);
        origin.Causal.Should().Be(0);
        origin.Modal.Should().Be(0);
    }

    [Fact]
    public void DimensionalCoordinate_DistanceTo_SamePoint_IsZero()
    {
        var coord = new DimensionalCoordinate(1, 2, 3, 4);

        coord.DistanceTo(coord).Should().Be(0);
    }

    [Fact]
    public void DimensionalCoordinate_DistanceTo_Origin_CalculatesCorrectly()
    {
        // sqrt(1^2 + 0^2 + 0^2 + 0^2) = 1
        var coord = new DimensionalCoordinate(1, 0, 0, 0);

        coord.DistanceTo(DimensionalCoordinate.Origin).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void DimensionalCoordinate_DistanceTo_CalculatesEuclideanDistance()
    {
        // sqrt((1-3)^2 + (2-4)^2 + (3-5)^2 + (4-6)^2) = sqrt(4+4+4+4) = sqrt(16) = 4
        var a = new DimensionalCoordinate(1, 2, 3, 4);
        var b = new DimensionalCoordinate(3, 4, 5, 6);

        a.DistanceTo(b).Should().BeApproximately(4.0, 0.001);
    }

    [Fact]
    public void DimensionalCoordinate_DistanceTo_IsSymmetric()
    {
        var a = new DimensionalCoordinate(1, 2, 3, 4);
        var b = new DimensionalCoordinate(5, 6, 7, 8);

        a.DistanceTo(b).Should().BeApproximately(b.DistanceTo(a), 0.001);
    }

    [Fact]
    public void DimensionalCoordinate_Origin_IsSingleton()
    {
        DimensionalCoordinate.Origin.Should().BeSameAs(DimensionalCoordinate.Origin);
    }

    // --- HypergridAnalysis ---

    [Fact]
    public void HypergridAnalysis_DefaultConstructor_HasZeroValues()
    {
        var analysis = new HypergridAnalysis();

        analysis.TemporalSpan.Should().Be(0);
        analysis.SemanticBreadth.Should().Be(0);
        analysis.CausalDepth.Should().Be(0);
        analysis.ModalRequirements.Should().BeEmpty();
        analysis.OverallComplexity.Should().Be(0);
    }

    [Fact]
    public void HypergridAnalysis_FullConstructor_SetsProperties()
    {
        var requirements = new List<string> { "approval", "tool" };
        var analysis = new HypergridAnalysis(1.5, 0.8, 3, requirements, 2.3);

        analysis.TemporalSpan.Should().Be(1.5);
        analysis.SemanticBreadth.Should().Be(0.8);
        analysis.CausalDepth.Should().Be(3);
        analysis.ModalRequirements.Should().HaveCount(2);
        analysis.OverallComplexity.Should().Be(2.3);
    }
}
