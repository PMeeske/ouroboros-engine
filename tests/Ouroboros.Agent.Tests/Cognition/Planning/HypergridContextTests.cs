using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Xunit;

namespace Ouroboros.Tests.Cognition.Planning;

[Trait("Category", "Unit")]
public class HypergridContextTests
{
    [Fact]
    public void Default_ShouldHaveNullDeadline()
    {
        HypergridContext.Default.Deadline.Should().BeNull();
    }

    [Fact]
    public void Default_ShouldHaveEmptySkillsAndTools()
    {
        HypergridContext.Default.AvailableSkills.Should().BeEmpty();
        HypergridContext.Default.AvailableTools.Should().BeEmpty();
    }

    [Fact]
    public void Default_RiskThreshold_ShouldBe07()
    {
        HypergridContext.Default.RiskThreshold.Should().Be(0.7);
    }

    [Fact]
    public void Create_WithCustomValues_ShouldSetProperties()
    {
        var deadline = DateTimeOffset.UtcNow.AddHours(1);
        var skills = new List<string> { "coding", "analysis" };
        var tools = new List<string> { "compiler" };
        var context = new HypergridContext(deadline, skills, tools, 0.5);

        context.Deadline.Should().Be(deadline);
        context.AvailableSkills.Should().HaveCount(2);
        context.AvailableTools.Should().HaveCount(1);
        context.RiskThreshold.Should().Be(0.5);
    }
}

[Trait("Category", "Unit")]
public class DimensionalCoordinateTests
{
    [Fact]
    public void Origin_ShouldBeAllZeros()
    {
        var origin = DimensionalCoordinate.Origin;

        origin.Temporal.Should().Be(0);
        origin.Semantic.Should().Be(0);
        origin.Causal.Should().Be(0);
        origin.Modal.Should().Be(0);
    }

    [Fact]
    public void DistanceTo_SamePoint_ShouldBeZero()
    {
        var point = new DimensionalCoordinate(1, 2, 3, 4);

        point.DistanceTo(point).Should().Be(0);
    }

    [Fact]
    public void DistanceTo_Origin_ShouldCalculateCorrectly()
    {
        var point = new DimensionalCoordinate(3, 0, 4, 0);

        point.DistanceTo(DimensionalCoordinate.Origin).Should().Be(5.0);
    }

    [Fact]
    public void DistanceTo_ShouldBeCommutative()
    {
        var a = new DimensionalCoordinate(1, 2, 3, 4);
        var b = new DimensionalCoordinate(5, 6, 7, 8);

        a.DistanceTo(b).Should().Be(b.DistanceTo(a));
    }

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var coord = new DimensionalCoordinate(1.5, 2.5, 3.5, 4.5);

        coord.Temporal.Should().Be(1.5);
        coord.Semantic.Should().Be(2.5);
        coord.Causal.Should().Be(3.5);
        coord.Modal.Should().Be(4.5);
    }
}

[Trait("Category", "Unit")]
public class HypergridAnalysisTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetZeroValues()
    {
        var analysis = new HypergridAnalysis();

        analysis.TemporalSpan.Should().Be(0);
        analysis.SemanticBreadth.Should().Be(0);
        analysis.CausalDepth.Should().Be(0);
        analysis.ModalRequirements.Should().BeEmpty();
        analysis.OverallComplexity.Should().Be(0);
    }

    [Fact]
    public void Create_WithCustomValues_ShouldSetProperties()
    {
        var requirements = new List<string> { "approval", "tool" };
        var analysis = new HypergridAnalysis(10.5, 5.0, 3, requirements, 0.85);

        analysis.TemporalSpan.Should().Be(10.5);
        analysis.SemanticBreadth.Should().Be(5.0);
        analysis.CausalDepth.Should().Be(3);
        analysis.ModalRequirements.Should().HaveCount(2);
        analysis.OverallComplexity.Should().Be(0.85);
    }
}
