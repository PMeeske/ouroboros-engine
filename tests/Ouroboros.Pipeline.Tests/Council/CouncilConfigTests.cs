using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class CouncilConfigTests
{
    [Fact]
    public void Constructor_WithDefaults_HasExpectedValues()
    {
        // Act
        var config = new CouncilConfig();

        // Assert
        config.MaxRoundsPerPhase.Should().Be(3);
        config.ConsensusThreshold.Should().Be(0.7);
        config.TimeoutPerAgent.Should().BeNull();
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsAllProperties()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var config = new CouncilConfig(
            MaxRoundsPerPhase: 5,
            ConsensusThreshold: 0.9,
            TimeoutPerAgent: timeout,
            RequireUnanimity: true,
            EnableMinorityReport: false);

        // Assert
        config.MaxRoundsPerPhase.Should().Be(5);
        config.ConsensusThreshold.Should().Be(0.9);
        config.TimeoutPerAgent.Should().Be(timeout);
        config.RequireUnanimity.Should().BeTrue();
        config.EnableMinorityReport.Should().BeFalse();
    }

    [Fact]
    public void Default_ReturnsDefaultConfiguration()
    {
        // Act
        var config = CouncilConfig.Default;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(3);
        config.ConsensusThreshold.Should().Be(0.7);
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void Strict_ReturnsStrictConfiguration()
    {
        // Act
        var config = CouncilConfig.Strict;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(5);
        config.ConsensusThreshold.Should().Be(1.0);
        config.RequireUnanimity.Should().BeTrue();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void Fast_ReturnsFastConfiguration()
    {
        // Act
        var config = CouncilConfig.Fast;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(1);
        config.ConsensusThreshold.Should().Be(0.5);
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Act
        var config1 = new CouncilConfig(MaxRoundsPerPhase: 3, ConsensusThreshold: 0.7);
        var config2 = new CouncilConfig(MaxRoundsPerPhase: 3, ConsensusThreshold: 0.7);

        // Assert
        config1.Should().Be(config2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = CouncilConfig.Default;

        // Act
        var modified = original with { RequireUnanimity = true };

        // Assert
        modified.MaxRoundsPerPhase.Should().Be(3);
        modified.RequireUnanimity.Should().BeTrue();
    }
}
