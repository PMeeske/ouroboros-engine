using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class DebatePhaseTests
{
    [Theory]
    [InlineData(DebatePhase.Proposal, 0)]
    [InlineData(DebatePhase.Challenge, 1)]
    [InlineData(DebatePhase.Refinement, 2)]
    [InlineData(DebatePhase.Voting, 3)]
    [InlineData(DebatePhase.Synthesis, 4)]
    public void DebatePhase_HasExpectedIntegerValue(DebatePhase phase, int expectedValue)
    {
        // Assert
        ((int)phase).Should().Be(expectedValue);
    }

    [Fact]
    public void DebatePhase_HasExactlyFiveValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<DebatePhase>();

        // Assert
        values.Should().HaveCount(5);
    }

    [Fact]
    public void DebatePhase_AllValuesAreDefined()
    {
        // Assert
        Enum.IsDefined(DebatePhase.Proposal).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Challenge).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Refinement).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Voting).Should().BeTrue();
        Enum.IsDefined(DebatePhase.Synthesis).Should().BeTrue();
    }

    [Fact]
    public void DebatePhase_ToString_ReturnsExpectedNames()
    {
        // Assert
        DebatePhase.Proposal.ToString().Should().Be("Proposal");
        DebatePhase.Challenge.ToString().Should().Be("Challenge");
        DebatePhase.Refinement.ToString().Should().Be("Refinement");
        DebatePhase.Voting.ToString().Should().Be("Voting");
        DebatePhase.Synthesis.ToString().Should().Be("Synthesis");
    }
}
