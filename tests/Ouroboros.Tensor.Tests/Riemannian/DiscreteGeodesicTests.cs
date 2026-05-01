// <copyright file="DiscreteGeodesicTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Tensor.Riemannian;
using Xunit;

namespace Ouroboros.Tensor.Tests.Riemannian;

public sealed class DiscreteGeodesicTests
{
    private static LocalNeighborhoodGraph BuildLineGraph()
    {
        // a - b - c - d, all edges weight 1.
        LocalNeighborhoodGraph g = new();
        g.AddNode("a");
        g.AddNode("b");
        g.AddNode("c");
        g.AddNode("d");
        g.AddEdge("a", "b", 1f);
        g.AddEdge("b", "c", 1f);
        g.AddEdge("c", "d", 1f);
        return g;
    }

    [Fact]
    public void ComputeShortestPath_OnLineGraph_ReturnsExpectedPath()
    {
        var g = BuildLineGraph();
        var reasoner = new DiscreteGeodesicReasoner();

        var result = reasoner.ComputeShortestPath(g, "a", "d");

        result.IsValid.Should().BeTrue();
        result.PathNodes.Select(n => n.Value).Should().Equal("a", "b", "c", "d");
        result.ComputedPathCost.Should().Be(3f);
        result.SegmentCosts.Should().Equal(1f, 1f, 1f);
    }

    [Fact]
    public void ComputeShortestPath_PrefersCheaperEdge()
    {
        LocalNeighborhoodGraph g = new();
        g.AddNode("a");
        g.AddNode("b");
        g.AddNode("c");
        g.AddEdge("a", "b", 5f);
        g.AddEdge("a", "c", 1f);
        g.AddEdge("c", "b", 1f);

        var reasoner = new DiscreteGeodesicReasoner();
        var result = reasoner.ComputeShortestPath(g, "a", "b");

        result.IsValid.Should().BeTrue();
        result.PathNodes.Select(n => n.Value).Should().Equal("a", "c", "b");
        result.ComputedPathCost.Should().Be(2f);
    }

    [Fact]
    public void ComputeShortestPath_WithIdenticalEndpoints_HasZeroCost()
    {
        var g = BuildLineGraph();
        var reasoner = new DiscreteGeodesicReasoner();

        var result = reasoner.ComputeShortestPath(g, "b", "b");

        result.IsValid.Should().BeTrue();
        result.ComputedPathCost.Should().Be(0f);
        result.PathNodes.Should().HaveCount(1);
    }

    [Fact]
    public void ComputeShortestPath_WithDisconnectedTarget_ReturnsInvalid()
    {
        LocalNeighborhoodGraph g = new();
        g.AddNode("a");
        g.AddNode("z");

        var reasoner = new DiscreteGeodesicReasoner();
        var result = reasoner.ComputeShortestPath(g, "a", "z");

        result.IsValid.Should().BeFalse();
        result.PathNodes.Should().BeEmpty();
        result.ComputedPathCost.Should().Be(float.PositiveInfinity);
    }

    [Fact]
    public void ComputeShortestPath_SatisfiesTriangleInequality()
    {
        LocalNeighborhoodGraph g = new();
        g.AddNode("a");
        g.AddNode("b");
        g.AddNode("c");
        g.AddEdge("a", "b", 2.5f);
        g.AddEdge("b", "c", 3.0f);
        g.AddEdge("a", "c", 10f);

        var reasoner = new DiscreteGeodesicReasoner();
        float ab = reasoner.ComputeShortestPath(g, "a", "b").ComputedPathCost;
        float bc = reasoner.ComputeShortestPath(g, "b", "c").ComputedPathCost;
        float ac = reasoner.ComputeShortestPath(g, "a", "c").ComputedPathCost;

        ac.Should().BeLessThanOrEqualTo(ab + bc + 1e-5f);
    }

    [Fact]
    public void ComputeShortestPath_IsSymmetric()
    {
        var g = BuildLineGraph();
        var reasoner = new DiscreteGeodesicReasoner();

        float ad = reasoner.ComputeShortestPath(g, "a", "d").ComputedPathCost;
        float da = reasoner.ComputeShortestPath(g, "d", "a").ComputedPathCost;

        ad.Should().Be(da);
    }
}
