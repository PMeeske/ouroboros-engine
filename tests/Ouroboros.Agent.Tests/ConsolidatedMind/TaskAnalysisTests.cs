using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class TaskAnalysisTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var secondaryRoles = new[] { SpecializedRole.Analyst };
        var caps = new[] { "code", "debug" };

        var analysis = new TaskAnalysis(
            SpecializedRole.CodeExpert,
            secondaryRoles,
            caps,
            EstimatedComplexity: 0.8,
            RequiresThinking: true,
            RequiresVerification: false,
            Confidence: 0.9);

        analysis.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
        analysis.SecondaryRoles.Should().Contain(SpecializedRole.Analyst);
        analysis.RequiredCapabilities.Should().Contain("code");
        analysis.EstimatedComplexity.Should().Be(0.8);
        analysis.RequiresThinking.Should().BeTrue();
        analysis.RequiresVerification.Should().BeFalse();
        analysis.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var roles = new[] { SpecializedRole.Analyst };
        var caps = new[] { "cap1" };
        var a = new TaskAnalysis(SpecializedRole.CodeExpert, roles, caps, 0.5, true, false, 0.8);
        var b = new TaskAnalysis(SpecializedRole.CodeExpert, roles, caps, 0.5, true, false, 0.8);

        a.Should().Be(b);
    }
}
