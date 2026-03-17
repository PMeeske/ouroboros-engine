using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentVoteTests
{
    [Fact]
    public void Create_WithValidParams_ReturnsVote()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var vote = AgentVote.Create(agentId, "OptionA", 0.8, "I prefer this");

        // Assert
        vote.AgentId.Should().Be(agentId);
        vote.Option.Should().Be("OptionA");
        vote.Confidence.Should().Be(0.8);
        vote.Reasoning.Should().Be("I prefer this");
        vote.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithoutReasoning_ReturnsVoteWithNullReasoning()
    {
        var vote = AgentVote.Create(Guid.NewGuid(), "OptionA", 0.5);
        vote.Reasoning.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullOption_ThrowsArgumentNullException()
    {
        Action act = () => AgentVote.Create(Guid.NewGuid(), null!, 0.5);
        act.Should().Throw<ArgumentNullException>().WithParameterName("option");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1.0)]
    public void Create_WithInvalidConfidence_ThrowsArgumentOutOfRangeException(double confidence)
    {
        Action act = () => AgentVote.Create(Guid.NewGuid(), "OptionA", confidence);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("confidence");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Create_WithBoundaryConfidence_Succeeds(double confidence)
    {
        var vote = AgentVote.Create(Guid.NewGuid(), "OptionA", confidence);
        vote.Confidence.Should().Be(confidence);
    }
}
