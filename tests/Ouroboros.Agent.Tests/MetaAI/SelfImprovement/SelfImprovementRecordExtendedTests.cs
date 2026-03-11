using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class HypothesisTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var supporting = new List<string> { "evidence1" };
        var counter = new List<string> { "counter1" };

        var hypothesis = new Hypothesis(id, "System X improves Y", "performance", 0.7, supporting, counter, now, false, null);

        hypothesis.Id.Should().Be(id);
        hypothesis.Statement.Should().Be("System X improves Y");
        hypothesis.Domain.Should().Be("performance");
        hypothesis.Confidence.Should().Be(0.7);
        hypothesis.SupportingEvidence.Should().HaveCount(1);
        hypothesis.CounterEvidence.Should().HaveCount(1);
        hypothesis.CreatedAt.Should().Be(now);
        hypothesis.Tested.Should().BeFalse();
        hypothesis.Validated.Should().BeNull();
    }

    [Fact]
    public void Create_WithValidated_ShouldSetIt()
    {
        var hypothesis = new Hypothesis(Guid.NewGuid(), "s", "d", 0.5, new List<string>(), new List<string>(), DateTime.UtcNow, true, true);

        hypothesis.Tested.Should().BeTrue();
        hypothesis.Validated.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var list = new List<string>();
        var a = new Hypothesis(id, "s", "d", 0.5, list, list, now, false, null);
        var b = new Hypothesis(id, "s", "d", 0.5, list, list, now, false, null);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class ExperimentTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var hypothesis = new Hypothesis(Guid.NewGuid(), "h", "d", 0.5, new List<string>(), new List<string>(), now, false, null);
        var steps = new List<PlanStep>();
        var outcomes = new Dictionary<string, object> { ["accuracy"] = (object)0.9 };

        var experiment = new Experiment(id, hypothesis, "Test accuracy", steps, outcomes, now);

        experiment.Id.Should().Be(id);
        experiment.Hypothesis.Should().Be(hypothesis);
        experiment.Description.Should().Be("Test accuracy");
        experiment.Steps.Should().BeEmpty();
        experiment.ExpectedOutcomes.Should().ContainKey("accuracy");
        experiment.DesignedAt.Should().Be(now);
    }
}
