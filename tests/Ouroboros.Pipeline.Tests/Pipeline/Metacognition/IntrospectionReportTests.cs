namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class IntrospectionReportTests
{
    [Fact]
    public void Empty_CreatesEmptyReport()
    {
        var state = InternalState.Initial();
        var report = IntrospectionReport.Empty(state);

        report.Observations.Should().BeEmpty();
        report.Anomalies.Should().BeEmpty();
        report.Recommendations.Should().BeEmpty();
        report.SelfAssessmentScore.Should().Be(0.5);
        report.HasAnomalies.Should().BeFalse();
        report.HasRecommendations.Should().BeFalse();
    }

    [Fact]
    public void WithObservation_AddsObservation()
    {
        var report = IntrospectionReport.Empty(InternalState.Initial())
            .WithObservation("High cognitive load detected");

        report.Observations.Should().HaveCount(1);
    }

    [Fact]
    public void WithAnomaly_AddsAnomalyAndSetsFlag()
    {
        var report = IntrospectionReport.Empty(InternalState.Initial())
            .WithAnomaly("Unexpected pattern");

        report.Anomalies.Should().HaveCount(1);
        report.HasAnomalies.Should().BeTrue();
    }

    [Fact]
    public void WithRecommendation_AddsRecommendationAndSetsFlag()
    {
        var report = IntrospectionReport.Empty(InternalState.Initial())
            .WithRecommendation("Reduce load");

        report.Recommendations.Should().HaveCount(1);
        report.HasRecommendations.Should().BeTrue();
    }
}
