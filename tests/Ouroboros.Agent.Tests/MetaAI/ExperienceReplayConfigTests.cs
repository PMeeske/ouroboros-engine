using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ExperienceReplayConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new ExperienceReplayConfig();

        // Assert
        sut.BatchSize.Should().Be(10);
        sut.MinQualityScore.Should().Be(0.6);
        sut.MaxExperiences.Should().Be(100);
        sut.PrioritizeHighQuality.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var sut = new ExperienceReplayConfig(
            BatchSize: 32,
            MinQualityScore: 0.8,
            MaxExperiences: 500,
            PrioritizeHighQuality: false);

        // Assert
        sut.BatchSize.Should().Be(32);
        sut.MinQualityScore.Should().Be(0.8);
        sut.MaxExperiences.Should().Be(500);
        sut.PrioritizeHighQuality.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new ExperienceReplayConfig();
        var b = new ExperienceReplayConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new ExperienceReplayConfig();

        // Act
        var modified = original with { BatchSize = 64 };

        // Assert
        modified.BatchSize.Should().Be(64);
        modified.MinQualityScore.Should().Be(0.6);
    }
}
