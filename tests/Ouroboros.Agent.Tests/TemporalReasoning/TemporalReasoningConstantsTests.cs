using FluentAssertions;
using Ouroboros.Agent.TemporalReasoning;
using Xunit;

namespace Ouroboros.Tests.TemporalReasoning;

[Trait("Category", "Unit")]
public class TemporalReasoningConstantsTests
{
    [Fact]
    public void MaxRelationLookahead_ShouldBeFive()
    {
        TemporalReasoningConstants.MaxRelationLookahead.Should().Be(5);
    }

    [Fact]
    public void MaxCausalityWindowMinutes_ShouldBeSixty()
    {
        TemporalReasoningConstants.MaxCausalityWindowMinutes.Should().Be(60.0);
    }
}
