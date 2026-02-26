namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class DebatePhaseTests
{
    [Fact]
    public void Enum_ContainsAllExpectedValues()
    {
        Enum.GetValues<DebatePhase>().Should().HaveCount(5);
        Enum.IsDefined(DebatePhase.Proposal).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Challenge).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Refinement).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Voting).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Synthesis).Should().BeTrue();
    }
}
