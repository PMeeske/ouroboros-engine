using System.Collections.Immutable;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class BranchReasoningSummaryTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var stepsByKind = ImmutableDictionary<string, int>.Empty
            .Add("Draft", 3)
            .Add("Critique", 2);

        var summary = new BranchReasoningSummary(
            "main-branch", 5, stepsByKind, 10, TimeSpan.FromSeconds(30));

        summary.BranchName.Should().Be("main-branch");
        summary.TotalSteps.Should().Be(5);
        summary.StepsByKind.Should().HaveCount(2);
        summary.TotalToolCalls.Should().Be(10);
        summary.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
    }
}
