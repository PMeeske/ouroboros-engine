using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class CacheStatisticsTests
{
    [Fact]
    public void UtilizationPercent_WithPositiveMaxEntries_ReturnsCorrectPercentage()
    {
        var stats = new CacheStatistics(50, 100, 200, 50, 0.8, 1024);

        stats.UtilizationPercent.Should().Be(50.0);
    }

    [Fact]
    public void UtilizationPercent_WithZeroMaxEntries_ReturnsZero()
    {
        var stats = new CacheStatistics(0, 0, 0, 0, 0.0, 0);

        stats.UtilizationPercent.Should().Be(0.0);
    }

    [Fact]
    public void UtilizationPercent_WhenFull_ReturnsHundred()
    {
        var stats = new CacheStatistics(200, 200, 500, 100, 0.83, 4096);

        stats.UtilizationPercent.Should().Be(100.0);
    }

    [Fact]
    public void IsHealthy_WithHighHitRate_ReturnsTrue()
    {
        var stats = new CacheStatistics(50, 100, 300, 100, 0.75, 1024);

        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_WithLowHitRateButWarmingUp_ReturnsTrue()
    {
        var stats = new CacheStatistics(10, 100, 20, 30, 0.4, 512);

        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_WithLowHitRateAndWarmedUp_ReturnsFalse()
    {
        var stats = new CacheStatistics(50, 100, 30, 80, 0.27, 1024);

        stats.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new CacheStatistics(10, 100, 50, 10, 0.83, 512);
        var b = new CacheStatistics(10, 100, 50, 10, 0.83, 512);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class CostBenefitAnalysisTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var cba = new CostBenefitAnalysis("cloud", 0.05, 0.95, 19.0, "Cloud offers best value");

        cba.RecommendedRoute.Should().Be("cloud");
        cba.EstimatedCost.Should().Be(0.05);
        cba.EstimatedQuality.Should().Be(0.95);
        cba.ValueScore.Should().Be(19.0);
        cba.Rationale.Should().Be("Cloud offers best value");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new CostBenefitAnalysis("local", 0.01, 0.7, 70.0, "Cheap");
        var b = new CostBenefitAnalysis("local", 0.01, 0.7, 70.0, "Cheap");

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class NextNodeCandidateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var candidate = new NextNodeCandidate("node-1", "Analyze", 0.85);

        candidate.NodeId.Should().Be("node-1");
        candidate.Action.Should().Be("Analyze");
        candidate.Confidence.Should().Be(0.85);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new NextNodeCandidate("n1", "Act", 0.5);
        var b = new NextNodeCandidate("n1", "Act", 0.5);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class MetaLearnerConfigTests
{
    [Fact]
    public void ParameterlessConstructor_SetsDefaults()
    {
        var config = new MetaLearnerConfig();

        config.MinEpisodesForOptimization.Should().Be(10);
        config.MaxFewShotExamples.Should().Be(5);
        config.MinConfidenceThreshold.Should().Be(0.6);
        config.DefaultEvaluationWindow.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsProperties()
    {
        var window = TimeSpan.FromHours(12);
        var config = new MetaLearnerConfig(20, 10, 0.8, window);

        config.MinEpisodesForOptimization.Should().Be(20);
        config.MaxFewShotExamples.Should().Be(10);
        config.MinConfidenceThreshold.Should().Be(0.8);
        config.DefaultEvaluationWindow.Should().Be(window);
    }
}
