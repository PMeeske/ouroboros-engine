namespace Ouroboros.Tests.Pipeline.Planning;

using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class PlanCandidateTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var plan = new Plan("test plan");
        var candidate = new PlanCandidate(plan, 0.85, "Best fit");

        candidate.Plan.Should().Be(plan);
        candidate.Score.Should().Be(0.85);
        candidate.Explanation.Should().Be("Best fit");
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var plan = new Plan("test");
        var c1 = new PlanCandidate(plan, 0.5, "ok");
        var c2 = new PlanCandidate(plan, 0.5, "ok");

        c1.Should().Be(c2);
    }
}
