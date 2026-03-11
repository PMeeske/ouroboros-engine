using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class SkillCompositionConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new SkillCompositionConfig();

        // Assert
        sut.MaxComponentSkills.Should().Be(5);
        sut.MinComponentQuality.Should().Be(0.7);
        sut.AllowRecursiveComposition.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var sut = new SkillCompositionConfig(
            MaxComponentSkills: 10,
            MinComponentQuality: 0.9,
            AllowRecursiveComposition: true);

        // Assert
        sut.MaxComponentSkills.Should().Be(10);
        sut.MinComponentQuality.Should().Be(0.9);
        sut.AllowRecursiveComposition.Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new SkillCompositionConfig();
        var b = new SkillCompositionConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new SkillCompositionConfig();

        // Act
        var modified = original with { AllowRecursiveComposition = true };

        // Assert
        modified.AllowRecursiveComposition.Should().BeTrue();
        modified.MaxComponentSkills.Should().Be(5);
    }
}
