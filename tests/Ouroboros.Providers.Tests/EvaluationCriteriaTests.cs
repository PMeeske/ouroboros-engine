namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class EvaluationCriteriaTests
{
    [Fact]
    public void Default_WeightsSumToOne()
    {
        var criteria = EvaluationCriteria.Default;
        var sum = criteria.RelevanceWeight + criteria.CoherenceWeight +
                  criteria.CompletenessWeight + criteria.LatencyWeight + criteria.CostWeight;
        sum.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void QualityFocused_PrioritizesRelevance()
    {
        var criteria = EvaluationCriteria.QualityFocused;
        criteria.RelevanceWeight.Should().Be(0.4);
    }

    [Fact]
    public void SpeedFocused_PrioritizesLatency()
    {
        var criteria = EvaluationCriteria.SpeedFocused;
        criteria.LatencyWeight.Should().Be(0.4);
    }

    [Fact]
    public void CostFocused_PrioritizesCost()
    {
        var criteria = EvaluationCriteria.CostFocused;
        criteria.CostWeight.Should().Be(0.4);
    }
}
