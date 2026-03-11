using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class CommitmentStatusTests
{
    [Theory]
    [InlineData(CommitmentStatus.Planned)]
    [InlineData(CommitmentStatus.InProgress)]
    [InlineData(CommitmentStatus.Completed)]
    [InlineData(CommitmentStatus.Failed)]
    [InlineData(CommitmentStatus.Cancelled)]
    [InlineData(CommitmentStatus.AtRisk)]
    public void CommitmentStatus_AllValues_AreDefined(CommitmentStatus status)
    {
        // Act & Assert
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Fact]
    public void CommitmentStatus_HasSixValues()
    {
        // Act
        var values = Enum.GetValues<CommitmentStatus>();

        // Assert
        values.Should().HaveCount(6);
    }

    [Fact]
    public void CommitmentStatus_Planned_HasValue0()
    {
        ((int)CommitmentStatus.Planned).Should().Be(0);
    }

    [Fact]
    public void CommitmentStatus_InProgress_HasValue1()
    {
        ((int)CommitmentStatus.InProgress).Should().Be(1);
    }

    [Fact]
    public void CommitmentStatus_Completed_HasValue2()
    {
        ((int)CommitmentStatus.Completed).Should().Be(2);
    }

    [Fact]
    public void CommitmentStatus_Failed_HasValue3()
    {
        ((int)CommitmentStatus.Failed).Should().Be(3);
    }

    [Fact]
    public void CommitmentStatus_Cancelled_HasValue4()
    {
        ((int)CommitmentStatus.Cancelled).Should().Be(4);
    }

    [Fact]
    public void CommitmentStatus_AtRisk_HasValue5()
    {
        ((int)CommitmentStatus.AtRisk).Should().Be(5);
    }
}
