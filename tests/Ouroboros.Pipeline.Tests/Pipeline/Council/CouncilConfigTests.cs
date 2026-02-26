namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class CouncilConfigTests
{
    [Fact]
    public void Default_HasSensibleDefaults()
    {
        var config = CouncilConfig.Default;

        config.MaxRoundsPerPhase.Should().Be(3);
        config.ConsensusThreshold.Should().Be(0.7);
        config.TimeoutPerAgent.Should().BeNull();
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void Strict_RequiresUnanimity()
    {
        var config = CouncilConfig.Strict;

        config.MaxRoundsPerPhase.Should().Be(5);
        config.ConsensusThreshold.Should().Be(1.0);
        config.RequireUnanimity.Should().BeTrue();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void Fast_HasReducedRounds()
    {
        var config = CouncilConfig.Fast;

        config.MaxRoundsPerPhase.Should().Be(1);
        config.ConsensusThreshold.Should().Be(0.5);
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeFalse();
    }
}
