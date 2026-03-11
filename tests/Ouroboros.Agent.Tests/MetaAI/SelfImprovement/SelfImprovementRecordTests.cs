using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class GoalConflictTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var goal1 = new Goal(Guid.NewGuid(), "Goal 1", GoalType.Primary, 1.0, null, new List<Goal>(), new Dictionary<string, object>(), DateTime.UtcNow, false, null);
        var goal2 = new Goal(Guid.NewGuid(), "Goal 2", GoalType.Safety, 0.9, null, new List<Goal>(), new Dictionary<string, object>(), DateTime.UtcNow, false, null);
        var resolutions = new List<string> { "Prioritize safety", "Merge goals" };

        var conflict = new GoalConflict(goal1, goal2, "ResourceContention", "Both need GPU", resolutions);

        conflict.Goal1.Should().Be(goal1);
        conflict.Goal2.Should().Be(goal2);
        conflict.ConflictType.Should().Be("ResourceContention");
        conflict.Description.Should().Be("Both need GPU");
        conflict.SuggestedResolutions.Should().HaveCount(2);
    }
}

[Trait("Category", "Unit")]
public class ExplorationOpportunityTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var prereqs = new List<string> { "prereq1" };
        var opportunity = new ExplorationOpportunity("Try new approach", 0.9, 0.75, prereqs, now);

        opportunity.Description.Should().Be("Try new approach");
        opportunity.NoveltyScore.Should().Be(0.9);
        opportunity.InformationGainEstimate.Should().Be(0.75);
        opportunity.Prerequisites.Should().HaveCount(1);
        opportunity.IdentifiedAt.Should().Be(now);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var now = DateTime.UtcNow;
        var prereqs = new List<string>();
        var a = new ExplorationOpportunity("d", 0.5, 0.5, prereqs, now);
        var b = new ExplorationOpportunity("d", 0.5, 0.5, prereqs, now);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class CuriosityEngineConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new CuriosityEngineConfig();

        config.ExplorationThreshold.Should().Be(0.6);
        config.ExploitationBias.Should().Be(0.7);
        config.MaxExplorationPerSession.Should().Be(5);
        config.EnableSafeExploration.Should().BeTrue();
        config.MinSafetyScore.Should().Be(0.8);
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new CuriosityEngineConfig(0.3, 0.5, 10, false, 0.5);

        config.ExplorationThreshold.Should().Be(0.3);
        config.ExploitationBias.Should().Be(0.5);
        config.MaxExplorationPerSession.Should().Be(10);
        config.EnableSafeExploration.Should().BeFalse();
        config.MinSafetyScore.Should().Be(0.5);
    }
}

[Trait("Category", "Unit")]
public class ImprovementPlanTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var actions = new List<string> { "refactor", "test" };
        var improvements = new Dictionary<string, double> { ["speed"] = 0.2, ["accuracy"] = 0.1 };
        var plan = new ImprovementPlan("Improve speed", actions, improvements, TimeSpan.FromHours(1), 0.9, now);

        plan.Goal.Should().Be("Improve speed");
        plan.Actions.Should().HaveCount(2);
        plan.ExpectedImprovements.Should().HaveCount(2);
        plan.EstimatedDuration.Should().Be(TimeSpan.FromHours(1));
        plan.Priority.Should().Be(0.9);
        plan.CreatedAt.Should().Be(now);
    }
}

[Trait("Category", "Unit")]
public class CitationMetadataTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var refs = new List<string> { "ref1", "ref2" };
        var citedBy = new List<string> { "paper1" };
        var citation = new CitationMetadata("p1", "AI Paper", 100, 25, refs, citedBy);

        citation.PaperId.Should().Be("p1");
        citation.Title.Should().Be("AI Paper");
        citation.CitationCount.Should().Be(100);
        citation.InfluentialCitationCount.Should().Be(25);
        citation.References.Should().HaveCount(2);
        citation.CitedBy.Should().HaveCount(1);
    }
}

[Trait("Category", "Unit")]
public class InsightTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var evidence = new List<string> { "obs1", "obs2" };
        var insight = new Insight("Performance", "Latency spikes at peak", 0.85, evidence, now);

        insight.Category.Should().Be("Performance");
        insight.Description.Should().Be("Latency spikes at peak");
        insight.Confidence.Should().Be(0.85);
        insight.SupportingEvidence.Should().HaveCount(2);
        insight.DiscoveredAt.Should().Be(now);
    }
}

[Trait("Category", "Unit")]
public class SelfAssessmentTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var scores = new Dictionary<string, double> { ["coding"] = 0.9, ["reasoning"] = 0.85 };
        var strengths = new List<string> { "Fast response" };
        var weaknesses = new List<string> { "Complex math" };
        var assessment = new SelfAssessment(0.87, 0.92, 0.75, scores, strengths, weaknesses, now, "Good overall");

        assessment.OverallPerformance.Should().Be(0.87);
        assessment.ConfidenceCalibration.Should().Be(0.92);
        assessment.SkillAcquisitionRate.Should().Be(0.75);
        assessment.CapabilityScores.Should().HaveCount(2);
        assessment.Strengths.Should().HaveCount(1);
        assessment.Weaknesses.Should().HaveCount(1);
        assessment.AssessmentTime.Should().Be(now);
        assessment.Summary.Should().Be("Good overall");
    }
}

[Trait("Category", "Unit")]
public class GoalHierarchyConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetExpectedDefaults()
    {
        var config = new GoalHierarchyConfig();

        config.MaxDepth.Should().Be(3);
        config.MaxSubgoalsPerGoal.Should().Be(5);
        config.SafetyConstraints.Should().HaveCount(4);
        config.SafetyConstraints.Should().Contain("Do not harm users");
        config.CoreValues.Should().HaveCount(4);
        config.CoreValues.Should().Contain("Helpfulness");
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var constraints = new List<string> { "Be safe" };
        var values = new List<string> { "Accuracy" };
        var config = new GoalHierarchyConfig(5, 10, constraints, values);

        config.MaxDepth.Should().Be(5);
        config.MaxSubgoalsPerGoal.Should().Be(10);
        config.SafetyConstraints.Should().HaveCount(1);
        config.CoreValues.Should().HaveCount(1);
    }
}

[Trait("Category", "Unit")]
public class ResearchPaperTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var date = new DateTime(2024, 1, 15);
        var paper = new ResearchPaper("p1", "Deep Learning", "Author A", "Abstract text", "AI", "https://example.com", date);

        paper.Id.Should().Be("p1");
        paper.Title.Should().Be("Deep Learning");
        paper.Authors.Should().Be("Author A");
        paper.Abstract.Should().Be("Abstract text");
        paper.Category.Should().Be("AI");
        paper.Url.Should().Be("https://example.com");
        paper.PublishedDate.Should().Be(date);
    }

    [Fact]
    public void Create_WithoutPublishedDate_ShouldDefaultToNull()
    {
        var paper = new ResearchPaper("p2", "T", "A", "Abs", "Cat", "url");

        paper.PublishedDate.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class SelfEvaluatorConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetExpectedDefaults()
    {
        var config = new SelfEvaluatorConfig();

        config.CalibrationSampleSize.Should().Be(100);
        config.MinConfidenceForPrediction.Should().Be(0.3);
        config.InsightGenerationBatchSize.Should().Be(20);
        config.PerformanceAnalysisWindow.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new SelfEvaluatorConfig(50, 0.5, 10, TimeSpan.FromDays(1));

        config.CalibrationSampleSize.Should().Be(50);
        config.MinConfidenceForPrediction.Should().Be(0.5);
        config.InsightGenerationBatchSize.Should().Be(10);
        config.PerformanceAnalysisWindow.Should().Be(TimeSpan.FromDays(1));
    }
}

[Trait("Category", "Unit")]
public class CapabilityRegistryConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new CapabilityRegistryConfig();

        config.MinSuccessRateThreshold.Should().Be(0.6);
        config.MinUsageCountForReliability.Should().Be(5);
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new CapabilityRegistryConfig(0.8, 10, TimeSpan.FromHours(24));

        config.MinSuccessRateThreshold.Should().Be(0.8);
        config.MinUsageCountForReliability.Should().Be(10);
        config.CapabilityExpirationTime.Should().Be(TimeSpan.FromHours(24));
    }
}

[Trait("Category", "Unit")]
public class PersistentMemoryConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new PersistentMemoryConfig();

        config.ShortTermCapacity.Should().Be(100);
        config.LongTermCapacity.Should().Be(1000);
        config.ConsolidationThreshold.Should().Be(0.7);
        config.EnableForgetting.Should().BeTrue();
        config.ForgettingThreshold.Should().Be(0.3);
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new PersistentMemoryConfig(50, 500, 0.5, TimeSpan.FromMinutes(30), false, 0.1);

        config.ShortTermCapacity.Should().Be(50);
        config.LongTermCapacity.Should().Be(500);
        config.ConsolidationThreshold.Should().Be(0.5);
        config.ConsolidationInterval.Should().Be(TimeSpan.FromMinutes(30));
        config.EnableForgetting.Should().BeFalse();
        config.ForgettingThreshold.Should().Be(0.1);
    }
}

[Trait("Category", "Unit")]
public class HypothesisEngineConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new HypothesisEngineConfig();

        config.MinConfidenceForTesting.Should().Be(0.3);
        config.MaxHypothesesPerDomain.Should().Be(10);
        config.EnableAbductiveReasoning.Should().BeTrue();
        config.AutoGenerateCounterExamples.Should().BeTrue();
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new HypothesisEngineConfig(0.5, 5, false, false);

        config.MinConfidenceForTesting.Should().Be(0.5);
        config.MaxHypothesesPerDomain.Should().Be(5);
        config.EnableAbductiveReasoning.Should().BeFalse();
        config.AutoGenerateCounterExamples.Should().BeFalse();
    }
}
